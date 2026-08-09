$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot
$sdkPath = Join-Path $env:LOCALAPPDATA "Android\Sdk"
$adbPath = Join-Path $sdkPath "platform-tools\adb.exe"
$defaultJbrPath = "C:\Program Files\Android\Android Studio\jbr"

# ---------------------------------------------------------------------------
# Node / npm availability check
# ---------------------------------------------------------------------------
try {
    $null = & node --version 2>&1
    if ($LASTEXITCODE -ne 0) { throw "node exit code $LASTEXITCODE" }
} catch {
    throw "找不到 node。請安裝 Node.js (https://nodejs.org) 並確認 node 在 PATH 中。錯誤：$_"
}

try {
    $null = & npm --version 2>&1
    if ($LASTEXITCODE -ne 0) { throw "npm exit code $LASTEXITCODE" }
} catch {
    throw "找不到 npm。請安裝 Node.js (https://nodejs.org) 並確認 npm 在 PATH 中。錯誤：$_"
}

# ---------------------------------------------------------------------------
# Android SDK check
# ---------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $adbPath)) {
    throw "找不到 Android SDK platform-tools：$adbPath"
}

# ---------------------------------------------------------------------------
# JAVA_HOME resolution
# ---------------------------------------------------------------------------
if ($env:JAVA_HOME -and (Test-Path -LiteralPath (Join-Path $env:JAVA_HOME "bin\java.exe"))) {
    $javaHomePath = $env:JAVA_HOME
} elseif (Test-Path -LiteralPath (Join-Path $defaultJbrPath "bin\java.exe")) {
    $javaHomePath = $defaultJbrPath
} else {
    throw "找不到有效的 Java JDK。請將 JAVA_HOME 設定為有效的 JDK 路徑（含 bin\java.exe），或安裝包含 JBR 的 Android Studio ($defaultJbrPath)。"
}

$env:ANDROID_HOME = $sdkPath
$env:JAVA_HOME = $javaHomePath

# ---------------------------------------------------------------------------
# H5 UI validation: install, lint, typecheck, test, build
# ---------------------------------------------------------------------------
Write-Host ">>> npm --prefix web-ui ci"
& npm --prefix web-ui ci
if ($LASTEXITCODE -ne 0) { throw "npm ci 失敗，exit code $LASTEXITCODE" }

Write-Host ">>> npm --prefix web-ui run lint"
& npm --prefix web-ui run lint
if ($LASTEXITCODE -ne 0) { throw "npm lint 失敗，exit code $LASTEXITCODE" }

Write-Host ">>> npm --prefix web-ui run typecheck"
& npm --prefix web-ui run typecheck
if ($LASTEXITCODE -ne 0) { throw "npm typecheck 失敗，exit code $LASTEXITCODE" }

Write-Host ">>> npm --prefix web-ui test -- --run"
& npm --prefix web-ui test -- --run
if ($LASTEXITCODE -ne 0) { throw "npm test 失敗，exit code $LASTEXITCODE" }

Write-Host ">>> npm --prefix web-ui run build"
& npm --prefix web-ui run build
if ($LASTEXITCODE -ne 0) { throw "npm build 失敗，exit code $LASTEXITCODE" }

# ---------------------------------------------------------------------------
# Android build: unit tests, assemble, lint
# (web-ui dist is already built above; Gradle syncWebAssets task will copy it)
# ---------------------------------------------------------------------------
Push-Location ".\android-app"
try {
    & ".\gradlew.bat" testDebugUnitTest assembleDebug lintDebug
    if ($LASTEXITCODE -ne 0) { throw "Android build failed with exit code $LASTEXITCODE" }
} finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# ADB install, reverse, start
# ---------------------------------------------------------------------------
& $adbPath start-server | Out-Null
& $adbPath install -r ".\android-app\app\build\outputs\apk\debug\app-debug.apk"
if ($LASTEXITCODE -ne 0) { throw "APK installation failed with exit code $LASTEXITCODE" }
& ".\scripts\Watch-AgentDeckAndroidReverse.ps1"
& $adbPath shell am force-stop com.agentdeck.mobile
& $adbPath shell monkey -p com.agentdeck.mobile -c android.intent.category.LAUNCHER 1 | Out-Null

Write-Host "AgentDeck 已安裝並在 Android 裝置上啟動。"
