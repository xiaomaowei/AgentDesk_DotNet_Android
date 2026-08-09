[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$slnPath = Join-Path $scriptDir "windows\AgentDesk.sln"

if (-not (Test-Path -LiteralPath $slnPath)) {
    Write-Error "Solution file not found: $slnPath"
    exit 1
}

$desktopProject = Join-Path $scriptDir "windows\AgentDesk.Desktop\AgentDesk.Desktop.csproj"
$desktopOutputDir = Join-Path $scriptDir "windows\artifacts\AgentDesk.Desktop-win-x64"

$hookProject = Join-Path $scriptDir "windows\AgentDesk.Hook\AgentDesk.Hook.csproj"
$hookOutputDir = Join-Path $scriptDir "windows\artifacts\AgentDesk.Hook-win-x64"

Write-Host "Publishing AgentDesk.Desktop (framework-dependent win-x64)..." -ForegroundColor Cyan
dotnet publish $desktopProject -c Release -r win-x64 --self-contained false -o $desktopOutputDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish AgentDesk.Desktop"
    exit $LASTEXITCODE
}

Write-Host "Publishing AgentDesk.Hook (framework-dependent win-x64)..." -ForegroundColor Cyan
dotnet publish $hookProject -c Release -r win-x64 --self-contained false -o $hookOutputDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish AgentDesk.Hook"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Publish completed successfully!" -ForegroundColor Green
Write-Host "Desktop Host Executable: $(Join-Path $desktopOutputDir 'AgentDesk.Desktop.exe')"
Write-Host "Hook Executable:         $(Join-Path $hookOutputDir 'AgentDesk.Hook.exe')"
Write-Host ""
Write-Host "Next Steps:"
Write-Host "1. Start AgentDesk Desktop application: $(Join-Path $desktopOutputDir 'AgentDesk.Desktop.exe')"
Write-Host "2. Ensure ADB reverse is active if using physical Android device: adb reverse tcp:8765 tcp:8765"
Write-Host "3. Approve/trust project hooks in Codex configuration if prompted."
Write-Host "4. Verify all seven hooks are active (SessionStart, SessionEnd, UserPromptSubmit, PreToolUse, PermissionRequest, PostToolUse, Stop)."
Write-Host "5. Start a new Codex task to trigger automatic lifecycle hooks."
