$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot
$adbPath = Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
if (-not (Test-Path -LiteralPath $adbPath)) {
    throw "找不到 Android SDK platform-tools：$adbPath"
}

& $adbPath start-server | Out-Null
$connectedDevices = & $adbPath devices
if (-not ($connectedDevices -match "\sdevice$")) {
    throw "找不到已授權的 Android USB debugging 裝置。請解鎖手機並允許這台電腦。"
}

# Restore ADB reverse rule (tcp:8765 -> tcp:8765)
& ".\scripts\Watch-AgentDeckAndroidReverse.ps1"

# Launch Android App (com.agentdeck.mobile)
& $adbPath shell monkey -p com.agentdeck.mobile -c android.intent.category.LAUNCHER 1 | Out-Null

$watcherPath = Join-Path $PSScriptRoot "scripts\Watch-AgentDeckAndroidReverse.ps1"
$reverseWatcher = Start-Job -ScriptBlock {
    param($scriptPath)
    & $scriptPath -Watch
} -ArgumentList $watcherPath

try {
    # Check .NET Server health
    try {
        $healthResponse = Invoke-RestMethod -Uri "http://127.0.0.1:8765/health" -TimeoutSec 2 -ErrorAction Stop
        Write-Host "AgentDesk .NET Server 連線正常：$($healthResponse | ConvertTo-Json -Compress)"
    } catch {
        Write-Warning "【警告】.NET Windows Server 尚未啟動！無法存取 http://127.0.0.1:8765/health。"
        Write-Warning "過渡狀態說明：舊 Python Bridge 已停用。請在安裝 .NET 8 SDK 後，於 windows/ 目錄下編譯並啟動 AgentDesk.Server 或 AgentDesk.Desktop。"
    }

    Write-Host "AgentDesk Android 應用程式已在裝置上啟動，ADB Reverse 通道已監控中。"
    Write-Host "按 Ctrl+C 可結束 reverse 通道背景監控。"
    
    while ($true) {
        Start-Sleep -Seconds 5
    }
} finally {
    Stop-Job -Job $reverseWatcher -ErrorAction SilentlyContinue
    Remove-Job -Job $reverseWatcher -Force -ErrorAction SilentlyContinue
}
