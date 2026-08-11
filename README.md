# AgentDesk (.NET & Android)

[English](README.md) · [繁體中文](README.zh-TW.md)

An agentic task execution deck providing real-time status monitoring, interactive approvals, and desktop hook integration via a native Android app, H5 WebView, and a high-performance **pure .NET 8 Windows Bridge**.

> **Project Status Notice**: This repository is the pure .NET 8 iteration of AgentDesk. The legacy Python bridge and ESP32 hardware firmware have been removed from this scope. Both the .NET 8 Windows Bridge and Samsung Galaxy S23 (SM-S9110) physical end-to-end (E2E) device acceptance are completed. The legacy repository (`AgentDesk`) remains as a reference and backup, but is no longer required as an unvalidated fallback. This repository serves as the primary home for ongoing development.

---

## 🏗 Scope & Architecture

AgentDesk (.NET & Android) consists of four primary layers:

```
┌─────────────────────────────────────────────────────────┐
│                     Android Device                      │
│  ┌───────────────────────────────────────────────────┐  │
│  │   Android App (Native Kotlin/Java Container)      │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │   H5 Hybrid UI (React / Vite / TypeScript)  │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────┘
                           │ ADB Reverse Tunnel (tcp:8765)
                           ▼
┌─────────────────────────────────────────────────────────┐
│                 Windows Host (127.0.0.1:8765)           │
│  ┌───────────────────────────────────────────────────┐  │
│  │              AgentDesk.Desktop (WinForms Tray)    │  │
│  └─────────────────────────┬─────────────────────────┘  │
│                            │ Controls                   │
│  ┌─────────────────────────▼─────────────────────────┐  │
│  │              AgentDesk.Server (ASP.NET Core)      │  │
│  │  - Loopback API (http://127.0.0.1:8765)           │  │
│  │  - Protocol Handlers (/api/v1/*)                  │  │
│  └─────────────────────────▲─────────────────────────┘  │
│                            │ Invokes                    │
│  ┌─────────────────────────┴─────────────────────────┐  │
│  │  AgentDesk.Hook (CLI / System Integration)       │  │
│  │  AgentDesk.Core (Shared Domain Logic & Models)    │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### Key Components
1. **`windows/` (.NET 8 Windows Bridge)**:
   - `AgentDesk.Core`: Domain models, protocol schemas, and state management.
   - `AgentDesk.Server`: Lightweight ASP.NET Core loopback server listening on `127.0.0.1:8765`.
   - `AgentDesk.Desktop`: Windows System Tray app managing embedded server lifecycle, Android launching, and native ADB reverse tunneling (`tcp:8765 tcp:8765`).
   - `AgentDesk.Hook`: CLI bridge for receiving Codex / agent hook callbacks (`/api/v1/hooks/codex`).
   - `tests/`: Unit test suites for the Core and Desktop projects.
2. **`android-app/`**: Native Android app hosting the WebView interface and communicating with the Windows Bridge via HTTP API over ADB reverse.
3. **`web-ui/`**: Modern React H5 dashboard embedded into Android WebView, displaying active turns, step progress, diffs, and action approval controls.
4. **`protocol/`**: Protocol v1 definitions, JSON schemas, and message payload examples.

---

## 📁 Directory Structure

```
.
├── AGENTS.md                   # Agent collaboration & workflow guidelines
├── Install-AgentDeckAndroid.ps1 # Script to validate H5, build APK, install & launch
├── Start-AgentDeckAndroid.ps1   # Script to manage ADB reverse tunnel & launch Android app
├── LICENSE
├── VERSION
├── android-app/                # Native Android Gradle project
├── docs/                       # Architecture, Migration, Android & Protocol docs
│   ├── android.md
│   ├── architecture.md
│   ├── migration.md
│   └── protocol.md
├── protocol/                   # Protocol schemas and example payloads
├── scripts/
│   └── Watch-AgentDeckAndroidReverse.ps1 # ADB reverse auto-restoration watcher
├── web-ui/                     # React/TypeScript H5 frontend codebase
└── windows/                    # .NET 8 Windows Bridge projects & solution
    ├── Directory.Build.props
    ├── README.md
    ├── AgentDesk.Core/
    ├── AgentDesk.Server/
    ├── AgentDesk.Desktop/
    ├── AgentDesk.Hook/
    └── tests/
        ├── AgentDesk.Core.Tests/
        └── AgentDesk.Desktop.Tests/
```

---

## 🚀 Setup & Usage

### Prerequisites
- **.NET 8 SDK**: Required only for building from source or running via `dotnet run`. (The published Windows package `AgentDesk-Windows-vX.Y.Z-win-x64.zip` is self-contained and does not require .NET Runtime to be installed on target machines).
- **Android SDK & platform-tools** (`adb.exe` in `PATH`, `ANDROID_HOME`, `ANDROID_SDK_ROOT`, or `%LOCALAPPDATA%\Android\Sdk\platform-tools`).
- **Node.js (20.19+) & npm** (for `web-ui` development and building assets).
- **JDK 17+** (for Android Gradle build).

### Available Commands

#### 1. Validate & Install Android App
```powershell
.\Install-AgentDeckAndroid.ps1
```
This script validates the `web-ui` (lint, typecheck, test, build), builds the Android debug APK via Gradle, installs it to the connected Android device, sets up ADB reverse port forwarding, and starts the app.

#### 2. Start Android App & Tunnel
```powershell
.\Start-AgentDeckAndroid.ps1
```
Ensures `adb reverse tcp:8765 tcp:8765` is maintained, starts the background watcher, launches the Android application, and checks `http://127.0.0.1:8765/health`. If the .NET Server is not yet running, a friendly warning is printed.

#### 3. Build, Publish & Run .NET Windows Desktop Tray App & Hook
Double-clicking the compiled `AgentDesk.Desktop.exe` runs silently in the system tray without a console window.

```powershell
# Publish both AgentDesk.Desktop and AgentDesk.Hook as win-x64 artifacts
.\Publish-AgentDeskWindows.ps1

# Restore & build solution
dotnet build windows/AgentDesk.sln -c Release

# Run Desktop System Tray App
dotnet run --project windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj -c Release

# Run solution tests
dotnet test windows/AgentDesk.sln --configuration Release --no-build
```

#### 4. Setup & Verify Codex Project Hooks
To enable real automatic lifecycle event forwarding from Codex tasks to AgentDesk:

1. **Publish Executables**: Run `.\Publish-AgentDeskWindows.ps1` to produce `windows/artifacts/AgentDesk.Desktop-win-x64/AgentDesk.Desktop.exe` and `windows/artifacts/AgentDesk.Hook-win-x64/AgentDesk.Hook.exe`.
2. **Start Desktop Host**: Launch `AgentDesk.Desktop.exe` (or via `dotnet run --project windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj`).
3. **Confirm ADB Reverse**: Ensure `adb reverse tcp:8765 tcp:8765` is active for connected Android devices.
4. **Approve Project Hooks**: Trust and approve project hooks when prompted by Codex to allow `.codex/hooks.json` execution.
5. **Verify Registered Hooks**: Confirm all 7 lifecycle hooks are active (`SessionStart`, `SessionEnd`, `UserPromptSubmit`, `PreToolUse`, `PermissionRequest`, `PostToolUse`, `Stop`).
6. **Start Real Task**: Launch a new Codex task to initiate automatic real-hook forwarding.

> **Verification Notice**:
> - **Synthetic Hook Verification**: Manual CLI invocation (`UserPromptSubmit` payload via `AgentDesk.Hook.exe`) was validated as an earlier lower-layer test.
> - **Automatic Real-Task Verification (2026-08-10)**: Real automatic lifecycle forwarding is verified on physical Samsung Galaxy S23 (SM-S9110 / RFCW607NEMH). All 7 hooks were configured in `.codex/hooks.json`, and 5 lifecycle events (`SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `Stop`) were automatically triggered during a real Codex CLI v0.147.0 task. Server state evolved `commentary:delivered` -> `tool:running` -> `tool:completed` -> `final:delivered` with token counts, and Android `SharedPreferences` independently verified phone-visible delivery for `Prompt=true`, `Commentary=true`, `CompletedTool=true`, `Final=true`, and `Tokens=true`. (`PermissionRequest` and automatic `SessionEnd` remain configured but unexercised).

---

## 📦 Automated GitHub Releases

AgentDesk uses GitHub Actions (`.github/workflows/release.yml`) to automatically build, test, package, and publish releases whenever a version tag `vX.Y.Z` (matching the root `VERSION` file) is pushed.

### GitHub Release Assets
Each tag-triggered Release publishes three assets:
1. `AgentDesk-Windows-vX.Y.Z-win-x64.zip`: Self-contained 64-bit Windows executables (`AgentDesk.Desktop.exe` and `AgentDesk.Hook.exe`).
2. `AgentDesk-Android-vX.Y.Z.apk`: Release-signed native Android APK containing the embedded `web-ui` H5 assets.
3. `SHA256SUMS.txt`: SHA-256 checksum file for verifying asset integrity.

### Installation Flow for Target Machine
1. **Windows Setup**:
   - Clone the repository.
   - Download `AgentDesk-Windows-vX.Y.Z-win-x64.zip` from the GitHub Release.
   - Extract the ZIP directly into the repository root directory. This creates:
     - `windows/artifacts/AgentDesk.Desktop-win-x64/`
     - `windows/artifacts/AgentDesk.Hook-win-x64/`
   - Launch `windows/artifacts/AgentDesk.Desktop-win-x64/AgentDesk.Desktop.exe`.
2. **Android Setup**:
   - Download `AgentDesk-Android-vX.Y.Z.apk` from the same GitHub Release.
   - Install the APK on your Android device (e.g. `adb install AgentDesk-Android-vX.Y.Z.apk`).
   - Connect the device via USB and ensure ADB reverse tunnel is active (`adb reverse tcp:8765 tcp:8765`).

### Triggering a Release
To publish a release:
1. Update `VERSION` (e.g. `0.1.0`).
2. Create and push a matching Git tag:
   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```
> **Note**: The workflow verifies that the tag matches `v` + `VERSION`. Mismatched tags fail before build or publication.

### Android Release Signing Secrets
Configure the following Repository Secrets in GitHub (`Settings -> Secrets and variables -> Actions`):
- `ANDROID_KEYSTORE_BASE64`: Base64-encoded string of your release `.keystore` / `.jks` file.
- `ANDROID_KEYSTORE_PASSWORD`: Keystore password.
- `ANDROID_KEY_ALIAS`: Key alias.
- `ANDROID_KEY_PASSWORD`: Key password.

> **Note**: All four signing secrets are strictly required. If any signing secret is missing, the release workflow will fail immediately and will never publish an unsigned APK.

---

## 🔒 Security & Loopback Isolation

The Windows Bridge strictly binds to loopback (`127.0.0.1:8765`) and does not listen on external network interfaces (`0.0.0.0`). Communication from the physical Android device is securely routed over local USB debugging via `adb reverse tcp:8765 tcp:8765`.
