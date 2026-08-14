[CmdletBinding()]
param(
    [switch]$SelfContained = $true,
    [switch]$SkipGitPull,
    [switch]$NoLaunch,
    [Alias('Force')]
    [switch]$ForceStop
)

$ErrorActionPreference = 'Stop'

function Get-AdbPort5037ListenerPid {
    try {
        $conns = @(Get-NetTCPConnection -LocalPort 5037 -State Listen -ErrorAction SilentlyContinue)
        if ($conns.Count -gt 0 -and $conns[0].OwningProcess) {
            return [int]$conns[0].OwningProcess
        }
    } catch {}

    try {
        $netstat = netstat -ano 2>$null | Where-Object { $_ -match ':5037\s+.*LISTENING\s+(\d+)' }
        if ($netstat) {
            foreach ($line in $netstat) {
                if ($line -match '\s+(\d+)\s*$') {
                    $pVal = [int]$Matches[1]
                    if ($pVal -gt 0) { return $pVal }
                }
            }
        }
    } catch {}

    return $null
}

function Get-ProcessExecutablePath {
    param([int]$ProcessId)

    try {
        $cimProc = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
        if ($cimProc -and $cimProc.ExecutablePath) {
            return [System.IO.Path]::GetFullPath($cimProc.ExecutablePath)
        }
    } catch {}

    try {
        $gp = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($gp -and $gp.Path) {
            return [System.IO.Path]::GetFullPath($gp.Path)
        }
    } catch {}

    return $null
}

function Find-AdbExecutable {
    $candidates = @()

    $pathCmd = Get-Command adb.exe -ErrorAction SilentlyContinue
    if (-not $pathCmd) {
        $pathCmd = Get-Command adb -ErrorAction SilentlyContinue
    }
    if ($pathCmd -and $pathCmd.Source) {
        $candidates += $pathCmd.Source
    }

    if ($env:ANDROID_HOME) {
        $candidates += Join-Path $env:ANDROID_HOME "platform-tools\adb.exe"
    }
    if ($env:ANDROID_SDK_ROOT) {
        $candidates += Join-Path $env:ANDROID_SDK_ROOT "platform-tools\adb.exe"
    }
    if ($env:LOCALAPPDATA) {
        $candidates += Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
    }

    foreach ($cand in $candidates) {
        if ($cand -and (Test-Path -LiteralPath $cand)) {
            try {
                return [System.IO.Path]::GetFullPath($cand)
            } catch {
                return $cand
            }
        }
    }
    return $null
}

function Invoke-AdbProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AdbPath,
        [Parameter(Mandatory = $true)]
        [string]$Arguments,
        [int]$TimeoutMilliseconds = 5000
    )

    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $AdbPath
    $pinfo.Arguments = $Arguments
    $pinfo.UseShellExecute = $false
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $pinfo

    $timedOut = $false
    $exitCode = -1
    $output = ""
    $error = ""

    try {
        if ($process.Start()) {
            $procId = $process.Id
            $outTask = $process.StandardOutput.ReadToEndAsync()
            $errTask = $process.StandardError.ReadToEndAsync()

            if (-not $process.WaitForExit($TimeoutMilliseconds)) {
                $timedOut = $true
                try {
                    if ($procId -gt 0) {
                        $tkStartInfo = New-Object System.Diagnostics.ProcessStartInfo
                        $tkStartInfo.FileName = "taskkill.exe"
                        $tkStartInfo.Arguments = "/F /T /PID $procId"
                        $tkStartInfo.CreateNoWindow = $true
                        $tkStartInfo.UseShellExecute = $false
                        $tkProc = [System.Diagnostics.Process]::Start($tkStartInfo)
                        if ($tkProc) {
                            $tkProc.WaitForExit(1000)
                            $tkProc.Dispose()
                        }
                    }
                } catch {}

                try {
                    if (-not $process.HasExited) {
                        $process.Kill()
                    }
                } catch {}
            } else {
                $exitCode = $process.ExitCode
            }

            if ($outTask -and $outTask.Wait(500)) {
                $output = $outTask.Result
            }
            if ($errTask -and $errTask.Wait(500)) {
                $error = $errTask.Result
            }
        }
    } catch {
        $error = $_.Exception.Message
    } finally {
        $process.Dispose()
    }

    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output   = $output
        Error    = $error
        TimedOut = $timedOut
    }
}

function Invoke-AdbRecovery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AdbPath
    )

    Write-Host "Recovering ADB server before Desktop launch..." -ForegroundColor Cyan

    $maxAttempts = 3
    $recovered = $false
    $lastErrorMsg = ""

    $killTimeoutMs = 5000
    $startTimeoutMs = 15000
    $devicesTimeoutMs = 10000

    $killTimeoutSec = [int]($killTimeoutMs / 1000)
    $startTimeoutSec = [int]($startTimeoutMs / 1000)
    $devicesTimeoutSec = [int]($devicesTimeoutMs / 1000)

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        if ($attempt -gt 1) {
            Write-Host "ADB recovery retry $attempt of $maxAttempts..." -ForegroundColor Yellow
        }

        $killRes = Invoke-AdbProcess -AdbPath $AdbPath -Arguments "kill-server" -TimeoutMilliseconds $killTimeoutMs
        if ($killRes.TimedOut) {
            Write-Warning "ADB kill-server timed out after $killTimeoutSec seconds."
        }

        $cleared = $false
        for ($i = 0; $i -lt 10; $i++) {
            $listenerPid = Get-AdbPort5037ListenerPid
            if (-not $listenerPid) {
                $cleared = $true
                break
            }
            Start-Sleep -Milliseconds 300
        }

        if (-not $cleared) {
            $listenerPid = Get-AdbPort5037ListenerPid
            if ($listenerPid) {
                $listenerExe = Get-ProcessExecutablePath -ProcessId $listenerPid
                if ($listenerExe -and [string]::Equals($listenerExe, $AdbPath, [StringComparison]::OrdinalIgnoreCase)) {
                    Write-Warning "ADB kill-server did not clear TCP port 5037. Force-stopping matching adb.exe (PID $listenerPid)..."
                    Stop-Process -Id $listenerPid -Force -ErrorAction SilentlyContinue
                    Start-Sleep -Milliseconds 500
                } else {
                    $procDesc = if ($listenerExe) { "$listenerExe (PID $listenerPid)" } else { "PID $listenerPid" }
                    $lastErrorMsg = "TCP port 5037 is held by an unrelated process: $procDesc. Cannot stop unrelated process."
                    Write-Warning $lastErrorMsg
                }
            }
        }

        $startRes = Invoke-AdbProcess -AdbPath $AdbPath -Arguments "start-server" -TimeoutMilliseconds $startTimeoutMs
        if ($startRes.TimedOut) {
            Write-Warning "ADB start-server timed out after $startTimeoutSec seconds."
        }
        Start-Sleep -Milliseconds 500

        $devicesRes = Invoke-AdbProcess -AdbPath $AdbPath -Arguments "devices -l" -TimeoutMilliseconds $devicesTimeoutMs

        if ($devicesRes.TimedOut) {
            $lastErrorMsg = "'adb devices -l' timed out after $devicesTimeoutSec seconds."
        } elseif ($devicesRes.ExitCode -eq 0) {
            $recovered = $true
            Write-Host "ADB server successfully recovered and responsive." -ForegroundColor Green
            break
        } else {
            $outputStr = ($devicesRes.Output + "`n" + $devicesRes.Error).Trim()
            $lastErrorMsg = "'adb devices -l' returned exit code $($devicesRes.ExitCode). Output: $outputStr"
        }
    }

    if (-not $recovered) {
        Write-Error "Unrecoverable ADB failure before Desktop launch.`n$lastErrorMsg`nPlease check ADB background processes, system port 5037, or device connections."
        exit 1
    }
}

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

# Step 3.6: Recover ADB server before launching Desktop
$adbExe = Find-AdbExecutable
if ($adbExe) {
    Invoke-AdbRecovery -AdbPath $adbExe
} else {
    Write-Warning "adb.exe could not be discovered in PATH, ANDROID_HOME, ANDROID_SDK_ROOT, or LOCALAPPDATA. Skipping ADB recovery."
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
