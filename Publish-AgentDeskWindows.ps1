[CmdletBinding()]
param(
    [switch]$SelfContained = $true,
    [switch]$SkipGitPull,
    [switch]$NoLaunch,
    [Alias('Force')]
    [switch]$ForceStop
)

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$slnPath = Join-Path $scriptDir "windows\AgentDesk.sln"

if (-not (Test-Path -LiteralPath $slnPath)) {
    Write-Error "Solution file not found: $slnPath"
    exit 1
}

$desktopProject = Join-Path $scriptDir "windows\AgentDesk.Desktop\AgentDesk.Desktop.csproj"
$desktopOutputDir = Join-Path $scriptDir "windows\artifacts\AgentDesk.Desktop-win-x64"
$desktopExePath = Join-Path $desktopOutputDir "AgentDesk.Desktop.exe"
$targetDesktopExePath = [System.IO.Path]::GetFullPath($desktopExePath)

$hookProject = Join-Path $scriptDir "windows\AgentDesk.Hook\AgentDesk.Hook.csproj"
$hookOutputDir = Join-Path $scriptDir "windows\artifacts\AgentDesk.Hook-win-x64"
$hookExePath = Join-Path $hookOutputDir "AgentDesk.Hook.exe"

# Step 1: Git repository check & pull (unless -SkipGitPull)
if (-not $SkipGitPull) {
    Write-Host "Verifying Git repository status for $scriptDir..." -ForegroundColor Cyan

    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if (-not $gitCmd) {
        Write-Error "Git command line tool is not available in PATH."
        exit 1
    }

    $isWorkTree = git -C "$scriptDir" rev-parse --is-inside-work-tree 2>$null
    if ($LASTEXITCODE -ne 0 -or ($isWorkTree -and $isWorkTree.Trim() -ne "true")) {
        Write-Error "Script repository directory ($scriptDir) is not inside a valid Git worktree."
        exit 1
    }

    $gitStatus = git -C "$scriptDir" status --porcelain 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to check Git status for repository at $scriptDir."
        exit 1
    }

    if ($gitStatus) {
        Write-Error "Repository at $scriptDir contains uncommitted tracked or untracked changes.`nPublishing with automatic git pull requires a clean worktree.`nPlease commit or stash your changes, or use -SkipGitPull for local development/validation."
        exit 1
    }

    Write-Host "Running git pull --ff-only in $scriptDir..." -ForegroundColor Cyan
    git -C "$scriptDir" pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        Write-Error "git pull --ff-only failed for repository at $scriptDir."
        exit $LASTEXITCODE
    }
} else {
    Write-Host "Skipping Git pull and cleanliness check (-SkipGitPull specified)." -ForegroundColor Yellow
}

# Step 2: Detect running AgentDesk.Desktop instances
$runningInstances = @()

try {
    $cimProcs = Get-CimInstance -ClassName Win32_Process -Filter "Name = 'AgentDesk.Desktop.exe'" -ErrorAction SilentlyContinue
    if ($cimProcs) {
        foreach ($cp in $cimProcs) {
            $pidVal = $cp.ProcessId
            $pPath = $cp.ExecutablePath
            if (-not $pPath) {
                $gp = Get-Process -Id $pidVal -ErrorAction SilentlyContinue
                if ($gp) { $pPath = $gp.Path }
            }

            $normPath = $null
            if ($pPath) {
                try {
                    $normPath = [System.IO.Path]::GetFullPath($pPath)
                } catch {
                    $normPath = $null
                }
            }

            $runningInstances += [PSCustomObject]@{
                Id   = $pidVal
                Path = $normPath
            }
        }
    }
} catch {
    $gps = Get-Process -Name "AgentDesk.Desktop" -ErrorAction SilentlyContinue
    if ($gps) {
        foreach ($gp in $gps) {
            $normPath = $null
            if ($gp.Path) {
                try {
                    $normPath = [System.IO.Path]::GetFullPath($gp.Path)
                } catch {
                    $normPath = $null
                }
            }

            $runningInstances += [PSCustomObject]@{
                Id   = $gp.Id
                Path = $normPath
            }
        }
    }
}

$targetProcesses = @()
$unrecognizedProcesses = @()

foreach ($inst in $runningInstances) {
    if ($inst.Path -and [string]::Equals($inst.Path, $targetDesktopExePath, [StringComparison]::OrdinalIgnoreCase)) {
        $targetProcesses += $inst
    } else {
        $unrecognizedProcesses += $inst
    }
}

if ($unrecognizedProcesses.Count -gt 0) {
    $procDetails = ($unrecognizedProcesses | ForEach-Object {
        $pStr = if ($_.Path) { $_.Path } else { "unknown/unreadable path" }
        "PID $($_.Id) ($pStr)"
    }) -join ", "

    Write-Error "Cannot safely republish because running AgentDesk.Desktop instance(s) from unknown or different path(s) were detected: $procDetails.`nPlease exit those instances manually before proceeding."
    exit 1
}

if ($targetProcesses.Count -gt 0) {
    $targetPidsStr = ($targetProcesses | ForEach-Object { $_.Id }) -join ", "
    if ($NoLaunch) {
        Write-Error "Target AgentDesk.Desktop (PID $targetPidsStr) is currently running at:`n  $targetDesktopExePath`nWith -NoLaunch specified, the target instance will not be automatically stopped. Publishing in-place while running is unsafe. Please exit AgentDesk.Desktop manually and try again."
        exit 1
    }

    Write-Warning "Target AgentDesk.Desktop instance(s) (PID $targetPidsStr) currently running at:`n  $targetDesktopExePath"
    Write-Warning "Stopping target instance(s) will clear in-memory sessions and active approvals."

    if (-not $ForceStop) {
        $confirm = Read-Host "Stop the running target instance(s) and proceed with republishing? [y/N]"
        if ($confirm -notmatch '^(?i:y|yes)$') {
            Write-Error "Operation cancelled by user. The running target instance was not stopped."
            exit 1
        }
    } else {
        Write-Host "Force-stopping running target instance(s) (-ForceStop specified)..." -ForegroundColor Yellow
    }

    foreach ($tp in $targetProcesses) {
        Write-Host "Stopping target AgentDesk.Desktop (PID $($tp.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $tp.Id -Force -ErrorAction SilentlyContinue
    }

    foreach ($tp in $targetProcesses) {
        $stopped = $false
        for ($i = 0; $i -lt 20; $i++) {
            $checkProc = Get-Process -Id $tp.Id -ErrorAction SilentlyContinue
            if (-not $checkProc) {
                $stopped = $true
                break
            }
            Start-Sleep -Milliseconds 500
        }

        if (-not $stopped) {
            Write-Error "Failed to stop running target AgentDesk.Desktop instance (PID $($tp.Id))."
            exit 1
        }
    }
}

# Step 3: Publish executables
$webUiDir = Join-Path $scriptDir "web-ui"
if (Test-Path -LiteralPath (Join-Path $webUiDir "package.json")) {
    Write-Host "Installing web-ui dependencies..." -ForegroundColor Cyan
    npm --prefix "$webUiDir" ci
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install web-ui dependencies via npm ci."
        exit $LASTEXITCODE
    }

    Write-Host "Building web-ui H5 assets..." -ForegroundColor Cyan
    npm --prefix "$webUiDir" run build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build web-ui H5 assets."
        exit $LASTEXITCODE
    }
}

$selfContainedBoolStr = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publishing AgentDesk.Desktop (win-x64 self-contained: $selfContainedBoolStr)..." -ForegroundColor Cyan
dotnet publish "$desktopProject" -c Release -r win-x64 --self-contained $selfContainedBoolStr -o "$desktopOutputDir"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish AgentDesk.Desktop"
    exit $LASTEXITCODE
}

Write-Host "Publishing AgentDesk.Hook (win-x64 self-contained: $selfContainedBoolStr)..." -ForegroundColor Cyan
dotnet publish "$hookProject" -c Release -r win-x64 --self-contained $selfContainedBoolStr -o "$hookOutputDir"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish AgentDesk.Hook"
    exit $LASTEXITCODE
}

if ($NoLaunch) {
    Write-Host ""
    Write-Host "Publish completed successfully (-NoLaunch specified, skipping launch and health check)." -ForegroundColor Green
    Write-Host "Desktop Host Executable: $desktopExePath"
    Write-Host "Hook Executable:         $hookExePath"
    exit 0
}

# Step 4: Launch newly published AgentDesk.Desktop.exe
Write-Host "Launching newly published AgentDesk.Desktop.exe..." -ForegroundColor Cyan
if (-not (Test-Path -LiteralPath $desktopExePath)) {
    Write-Error "Published executable not found at: $desktopExePath"
    exit 1
}

$launchedProcess = Start-Process -FilePath $desktopExePath -WorkingDirectory $desktopOutputDir -PassThru
if (-not $launchedProcess) {
    Write-Error "Failed to start process: $desktopExePath"
    exit 1
}

# Step 5: Bounded health check
Write-Host "Verifying embedded server health (http://127.0.0.1:8765/health)..." -ForegroundColor Cyan
$healthy = $false
$timeoutSeconds = 15
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

while ($stopwatch.Elapsed.TotalSeconds -lt $timeoutSeconds) {
    if ($launchedProcess.HasExited) {
        Write-Error "AgentDesk.Desktop process (PID $($launchedProcess.Id)) exited prematurely with exit code $($launchedProcess.ExitCode)."
        exit 1
    }

    try {
        $healthResp = Invoke-RestMethod -Uri "http://127.0.0.1:8765/health" -TimeoutSec 2 -ErrorAction Stop
        if ($healthResp -and ($healthResp.status -eq "ok" -or $healthResp.status -eq "OK")) {
            $healthy = $true
            break
        }
    }
    catch {
        # Server initializing, retry
    }

    Start-Sleep -Milliseconds 500
}

if (-not $healthy) {
    Write-Error "Health check timed out after $timeoutSeconds seconds. Server at http://127.0.0.1:8765/health did not respond with status ok."
    exit 1
}

Write-Host ""
Write-Host "Publish and launch completed successfully!" -ForegroundColor Green
Write-Host "Desktop Host Executable: $desktopExePath"
Write-Host "Hook Executable:         $hookExePath"
Write-Host "Server Health:           OK (http://127.0.0.1:8765/health)"
Write-Host ""
Write-Host "Next Steps:"
Write-Host "1. Ensure ADB reverse is active if using physical Android device: adb reverse tcp:8765 tcp:8765"
Write-Host "2. Approve/trust project hooks in Codex configuration if prompted."
Write-Host "3. Verify all seven hooks are active (SessionStart, SessionEnd, UserPromptSubmit, PreToolUse, PermissionRequest, PostToolUse, Stop)."
Write-Host "4. Start a new Codex task to trigger automatic lifecycle hooks."
