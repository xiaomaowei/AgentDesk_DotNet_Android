# AgentDesk Android Application Guide (.NET Bridge Transition)

This document provides instructions for building, installing, and running the native Android application container (`android-app/`) with the **.NET 8 Windows Bridge**.

---

## 📱 Component Architecture

```
┌────────────────────────────────────────────────────────┐
│                   Android Device                       │
│  ┌──────────────────────────────────────────────────┐  │
│  │   Android App (com.agentdeck.mobile)             │  │
│  │   - MainActivity                                 │  │
│  │   - AgentDeckView (Android WebView Container)    │  │
│  │   - H5Bridge (JavascriptInterface bridge)        │  │
│  └────────────────────────┬─────────────────────────┘  │
└───────────────────────────┼────────────────────────────┘
                            │ ADB Reverse (tcp:8765 -> tcp:8765)
                            ▼
┌────────────────────────────────────────────────────────┐
│           Windows Host (.NET 8 Bridge)                 │
│  - AgentDesk.Server listening on http://127.0.0.1:8765  │
└────────────────────────────────────────────────────────┘
```

The Android app embeds the `web-ui` SPA inside an Android `WebView`. The H5 application communicates with the host Windows Bridge via HTTP API endpoints (`http://127.0.0.1:8765/api/v1/...`) routed through ADB reverse port forwarding (`tcp:8765 tcp:8765`).

---

## 🛠 Prerequisites

1. **Android Device**:
   - USB Debugging enabled.
   - Authorized connection to the Windows host machine.
2. **Android SDK & platform-tools**:
   - `adb.exe` located in `PATH` or `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`.
3. **JDK 17+**:
   - `JAVA_HOME` pointing to a valid JDK (or Android Studio JBR).
4. **Node.js (v18+) & npm**:
   - For building `web-ui` static assets copied into Android app assets.
5. **.NET 8 SDK** *(Transition requirement)*:
   - Required to run the host `AgentDesk.Server` / `AgentDesk.Desktop`.

> **Note on Transition Status**: The legacy Python bridge virtual environment (`.venv`) is deprecated and disabled. Do **not** attempt to setup or launch Python venv environments. The host server must be launched using the .NET 8 Windows Bridge (`windows/` project solution).

---

## 🚀 Installation & Running

### 1. Full Build & Install Script
Run the automated installation script from the repository root:
```powershell
.\Install-AgentDeckAndroid.ps1
```
This script performs:
- H5 UI validation (`npm ci`, `lint`, `typecheck`, `test`, `build`).
- Android Gradle compilation (`gradlew.bat testDebugUnitTest assembleDebug lintDebug`).
- APK installation to connected device (`adb install -r`).
- ADB reverse tunnel configuration (`adb reverse tcp:8765 tcp:8765`).
- Launching the app on the device.

### 2. Launching App & Tunnel Watcher
To establish the reverse tunnel and launch the Android application without re-building:
```powershell
.\Start-AgentDeckAndroid.ps1
```
This script starts the background watcher (`Watch-AgentDeckAndroidReverse.ps1`) to ensure `tcp:8765` tunneling remains active and checks server health at `http://127.0.0.1:8765/health`. If the .NET Server is not running, a warning will be displayed.

---

## 🔧 Package & Activity Identifiers

- **Package Name**: `com.agentdeck.mobile`
- **Main Activity**: `com.agentdeck.mobile.MainActivity`
- **Port Forwarding**: `tcp:8765 tcp:8765`
