[CmdletBinding()]
param(
    [switch]$Watch,
    [ValidateRange(1, 60)]
    [int]$PollSeconds = 2
)

$ErrorActionPreference = "Stop"
$adbPath = Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
if (-not (Test-Path -LiteralPath $adbPath)) {
    throw "找不到 Android SDK platform-tools：$adbPath"
}

function Set-AgentDeckReverse {
    $deviceLines = & $adbPath devices
    if ($LASTEXITCODE -ne 0) {
        throw "ADB devices failed with exit code $LASTEXITCODE"
    }

    foreach ($line in $deviceLines) {
        if ($line -notmatch "^(\S+)\s+device(?:\s|$)") {
            continue
        }
        $serial = $Matches[1]
        $rules = & $adbPath -s $serial reverse --list 2>$null
        if ($rules -match "tcp:8765\s+tcp:8765") {
            continue
        }

        & $adbPath -s $serial reverse tcp:8765 tcp:8765 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "ADB reverse failed for $serial with exit code $LASTEXITCODE"
        }
        Write-Host "AgentDeck ADB tunnel restored for $serial"
    }
}

do {
    try {
        Set-AgentDeckReverse
    } catch {
        if (-not $Watch) {
            throw
        }
        Write-Warning $_
    }

    if ($Watch) {
        Start-Sleep -Seconds $PollSeconds
    }
} while ($Watch)
