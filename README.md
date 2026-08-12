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
   - `AgentDesk.Desktop`: Windows System Tray app managing embedded server lifecycle, opt-in secondary display WebView2 dashboard, Android launching, and native ADB reverse tunneling (`tcp:8765 tcp:8765`).
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
- **.NET 8 SDK**: Required for building and publishing the Windows Bridge executables locally or running via `dotnet run`.
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
# Default publish workflow: verifies clean Git worktree, pulls latest changes (git pull --ff-only),
# stops running target instance (after warning/confirmation), publishes both executables in-place,
# automatically launches AgentDesk.Desktop.exe, and verifies http://127.0.0.1:8765/health
.\Publish-AgentDeskWindows.ps1

# Local development / dirty worktree validation (skips git pull & cleanliness check)
.\Publish-AgentDeskWindows.ps1 -SkipGitPull

# Non-interactive publish and restart
.\Publish-AgentDeskWindows.ps1 -ForceStop

# Publish-only mode without stopping running instance or launching
.\Publish-AgentDeskWindows.ps1 -NoLaunch

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
   - After changing `.codex/hooks.json`, review and trust the project hooks again before starting a new Codex task.
5. **Verify Registered Hooks**: Confirm all 7 lifecycle hooks are active (`SessionStart`, `SessionEnd`, `UserPromptSubmit`, `PreToolUse`, `PermissionRequest`, `PostToolUse`, `Stop`).
6. **Start Real Task**: Launch a new Codex task to initiate automatic real-hook forwarding.

> **Verification Notice**:
> - **Synthetic Hook Verification**: Manual CLI invocation (`UserPromptSubmit` payload via `AgentDesk.Hook.exe`) was validated as an earlier lower-layer test.
> - **Automatic Real-Task Verification (2026-08-10)**: Real automatic lifecycle forwarding is verified on physical Samsung Galaxy S23 (SM-S9110 / RFCW607NEMH). All 7 hooks were configured in `.codex/hooks.json`, and 5 lifecycle events (`SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `Stop`) were automatically triggered during a real Codex CLI v0.147.0 task. Server state evolved `commentary:delivered` -> `tool:running` -> `tool:completed` -> `final:delivered` with token counts, and Android `SharedPreferences` independently verified phone-visible delivery for `Prompt=true`, `Commentary=true`, `CompletedTool=true`, `Final=true`, and `Tokens=true`. (`PermissionRequest` and automatic `SessionEnd` remain configured but unexercised).

---

## 📦 Source-Only Local Build & Distribution

This repository is distributed as **source-only**. Automated GitHub Actions release workflows and pre-built binaries have been removed. Users must clone the full repository and build both the Android APK and Windows executables locally.

### 1. Repository Setup
Clone the full repository before running any build commands:
```bash
git clone https://github.com/xiaomaowei/AgentDesk_DotNet_Android.git
cd AgentDesk_DotNet_Android
```

### 2. Building the Android Debug APK
The Android Gradle configuration automatically packages the compiled `web-ui` H5 assets during build. Therefore, `web-ui` npm dependencies must be installed prior to running the Gradle build.

Run the following commands from the repository root:

```bash
# 1. Install H5 UI dependencies required for asset synchronization
npm --prefix web-ui ci

# 2. Build the Android Debug APK using Gradle
# On Windows (PowerShell / CMD):
.\android-app\gradlew.bat -p android-app :app:assembleDebug

# On Linux / macOS:
./android-app/gradlew -p android-app :app:assembleDebug
```

#### Android APK Output Path
Upon successful completion, the compiled Debug APK will be located at:
```
android-app/app/build/outputs/apk/debug/app-debug.apk
```

> **Optional Signed Release Builds**: To produce a signed release APK locally, set your own keystore credentials (`ANDROID_KEYSTORE_PATH`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`) as Gradle properties or environment variables (`ORG_GRADLE_PROJECT_*`), and run `.\android-app\gradlew.bat -p android-app :app:assembleRelease` (Windows) or `./android-app/gradlew -p android-app :app:assembleRelease` (Linux/macOS).

### 3. Publishing Windows Binaries
Publish self-contained 64-bit Windows executables for both **AgentDesk.Desktop** and **AgentDesk.Hook** using explicit `dotnet publish` commands:

```powershell
# Publish AgentDesk.Desktop (Release, win-x64, self-contained)
dotnet publish windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj -c Release -r win-x64 --self-contained -o windows/artifacts/AgentDesk.Desktop-win-x64

# Publish AgentDesk.Hook (Release, win-x64, self-contained)
dotnet publish windows/AgentDesk.Hook/AgentDesk.Hook.csproj -c Release -r win-x64 --self-contained -o windows/artifacts/AgentDesk.Hook-win-x64
```

> **Convenience Wrapper Script**: Running `.\Publish-AgentDeskWindows.ps1` automates the update, publish, launch, and health verification workflow.
> - **Default Workflow**: 1) Enforces a clean worktree & executes `git pull --ff-only`, 2) Detects and prompts to stop any running target `AgentDesk.Desktop.exe`, 3) Publishes both projects, 4) Launches `AgentDesk.Desktop.exe`, 5) Verifies server health at `http://127.0.0.1:8765/health`.
> - **Risk Notice**: Stopping a running instance clears in-memory session states and active approvals.
> - **Stable Shortcut Target**: Existing desktop shortcuts pointing to `windows/artifacts/AgentDesk.Desktop-win-x64/AgentDesk.Desktop.exe` remain valid as binaries are updated in-place at deterministic paths.
> - **Available Switches**:
>   - `-SkipGitPull`: Skip Git cleanliness check and `git pull` for local development with uncommitted changes.
>   - `-NoLaunch`: Publish-only mode (does not stop running instance, launch, or health check; fails if target is running).
>   - `-ForceStop` (or `-Force`): Bypass interactive confirmation prompt when stopping running target instance.
>   - `-SelfContained`: Set to `$false` for framework-dependent build (defaults to `$true`).

#### Artifact Output Directories & Launch Requirements
The deterministic artifact directories produced by the publish commands are:
- **Desktop Host Executable**: `windows/artifacts/AgentDesk.Desktop-win-x64/AgentDesk.Desktop.exe`
- **Hook Executable**: `windows/artifacts/AgentDesk.Hook-win-x64/AgentDesk.Hook.exe`

> **Execution & Hook Setup Requirements**:
> - Users must launch `AgentDesk.Desktop.exe` directly from its published artifact directory (`windows/artifacts/AgentDesk.Desktop-win-x64/AgentDesk.Desktop.exe`). Existing per-machine shortcuts targeting this executable path remain valid after republishing.
> - Both the **Desktop** (`AgentDesk.Desktop.exe`) and **Hook** (`AgentDesk.Hook.exe`) published artifacts are required for the tracked `.codex/hooks.json` integration to operate correctly.

---

## 🔒 Security & Loopback Isolation

The Windows Bridge strictly binds to loopback (`127.0.0.1:8765`) and does not listen on external network interfaces (`0.0.0.0`). Communication from the physical Android device is securely routed over local USB debugging via `adb reverse tcp:8765 tcp:8765`.
