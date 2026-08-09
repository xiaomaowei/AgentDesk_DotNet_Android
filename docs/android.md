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
│  - AgentDesk.Desktop System Tray App                   │
│  - Embedded AgentDesk.Server listening on 127.0.0.1:8765│
│  - Native ADB Manager maintaining reverse tunnel       │
└────────────────────────────────────────────────────────┘
```

The Android app embeds the `web-ui` SPA inside an Android `WebView`. The H5 application communicates with the host Windows Bridge via HTTP API endpoints (`http://127.0.0.1:8765/api/v1/...`) routed through ADB reverse port forwarding (`tcp:8765 tcp:8765`).

---

## 🛠 Prerequisites

1. **Android Device**:
   - USB Debugging enabled.
   - Authorized connection to the Windows host machine.
2. **Android SDK & platform-tools**:
   - `adb.exe` located in `PATH`, `ANDROID_HOME`, `ANDROID_SDK_ROOT`, or `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`.
3. **JDK 17+**:
   - `JAVA_HOME` pointing to a valid JDK (or Android Studio JBR).
4. **Node.js (v18+) & npm**:
   - For building `web-ui` static assets copied into Android app assets.
5. **.NET 8 SDK / Desktop Runtime**:
   - Building from source or running via `dotnet run` requires **.NET 8 SDK**.
   - Running only the published framework-dependent `AgentDesk.Desktop.exe` requires **.NET 8 Desktop Runtime**.

> **Note on Transition Status**: The legacy Python bridge virtual environment (`.venv`) is deprecated and disabled. The host server is launched using the .NET 8 Windows Bridge (`windows/AgentDesk.Desktop` System Tray App or standalone `windows/AgentDesk.Server`). `AgentDesk.Desktop` natively manages ADB reverse tunnels (`tcp:8765 tcp:8765`) and app launches in .NET.

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

### 2. Launching App & Tunnel via .NET Desktop Tray App
Launch the .NET System Tray App:
```powershell
dotnet run --project windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj -c Release
```
Or double-click `AgentDesk.Desktop.exe`. The tray app automatically hosts `AgentDesk.Server` on `127.0.0.1:8765`, maintains ADB reverse port forwarding (`tcp:8765 tcp:8765`), and provides a context menu option to **Connect & Launch Android**.

---

## 🔧 Package & Activity Identifiers

- **Package Name**: `com.agentdeck.mobile`
- **Main Activity**: `com.agentdeck.mobile.MainActivity`
- **Explicit Component**: `com.agentdeck.mobile/.MainActivity`
- **Port Forwarding**: `tcp:8765 tcp:8765`

---

## ✅ Verified Physical Device Acceptance (2026-08-10)

Physical end-to-end (E2E) integration and automatic Codex task lifecycle delivery have been independently verified on physical Android hardware connected to the .NET 8 Windows Bridge:

- **Target Device**: Samsung Galaxy S23 (Model `SM-S9110`, Serial `RFCW607NEMH`).
- **Application Package**: `com.agentdeck.mobile` (`versionName 0.1.0`, installed via `adb install -r`).
- **Host Desktop & Hook Runtime**: `AgentDesk.Desktop.exe` and `AgentDesk.Hook.exe` running from deterministic published artifact folders (`windows/artifacts/AgentDesk.Desktop-win-x64/` and `windows/artifacts/AgentDesk.Hook-win-x64/`). Embedded `AgentDesk.Server` listening on loopback `127.0.0.1:8765` with ADB reverse `tcp:8765 tcp:8765`.

### Verified E2E Results
1. **Installation & Build Verification**:
   - H5/Android validation/build passed separately.
   - Built debug APK installed successfully using `adb install -r` (`Success`).
2. **Automated Tunnel Recovery**: Manually removing the ADB port forwarding (`adb reverse --remove tcp:8765`) triggered the `AgentDesk.Desktop` watcher, which automatically re-established `tcp:8765 tcp:8765`.
3. **Desktop Tray Control**: Invoking **Connect & Launch Android** from the Windows system tray context menu brought `com.agentdeck.mobile/.MainActivity` into the active foreground on the S23 device.
4. **Synthetic Hook Delivery Verification**: Manually invoking `AgentDesk.Hook.exe` (synthetic `UserPromptSubmit` payload via CLI) dispatched payload to `AgentDesk.Server`, which rendered on the S23 H5 WebView interface, validating lower-layer CLI hook communication.
5. **Restart Server Confirmation Safety**: Restarting the server from tray prompted a safety confirmation dialog warning that active sessions/pending approvals will be cleared; selecting `No (N)` safely retained active envelope state and prompts.
6. **Automatic Real-Task Lifecycle Forwarding (Verified 2026-08-10)**:
   - All 7 lifecycle hooks (`SessionStart`, `SessionEnd`, `UserPromptSubmit`, `PreToolUse`, `PermissionRequest`, `PostToolUse`, `Stop`) were configured via tracked `.codex/hooks.json`.
   - A real Codex CLI v0.147.0 task automatically triggered 5 observed lifecycle events (`SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `Stop`). (`PermissionRequest` and automatic `SessionEnd` were configured but not exercised during this run).
   - Server state evolved through `commentary:delivered` -> `tool:running` -> `tool:completed` -> `final:delivered` and produced `conversation_tokens`.
   - Android `SharedPreferences` independently verified phone-visible field delivery: `Prompt=true`, `Commentary=true`, `CompletedTool=true`, `Final=true`, `Tokens=true`.

### 🔄 Automatic Real-Task Hook Setup & Execution Steps
To execute automatic real-task Codex hook delivery:
1. **Publish Artifacts**: Run `.\Publish-AgentDeskWindows.ps1` to publish both `AgentDesk.Desktop.exe` and `AgentDesk.Hook.exe` to deterministic folders.
2. **Start Desktop Host**: Run `AgentDesk.Desktop.exe` in Windows System Tray.
3. **Confirm Tunnel**: Ensure `adb reverse tcp:8765 tcp:8765` is connected.
4. **Approve Project Hooks**: Trust project `.codex/hooks.json` in Codex when prompted.
5. **Verify Registered Hooks**: Confirm all 7 lifecycle hooks (`SessionStart`, `SessionEnd`, `UserPromptSubmit`, `PreToolUse`, `PermissionRequest`, `PostToolUse`, `Stop`) are active.
6. **Start Codex Task**: Initiate a new Codex task to trigger real automatic lifecycle forwarding.

> *Note: Real automatic Codex-to-S23 lifecycle forwarding has been fully verified on physical hardware for the 5 observed lifecycle events and phone-visible fields.*
