# AgentDesk (.NET & Android)

An agentic task execution deck providing real-time status monitoring, interactive approvals, and desktop hook integration via a native Android app, H5 WebView, and a high-performance **pure .NET 8 Windows Bridge**.

> **Project Status Notice**: This repository is the updated, pure .NET 8 iteration of AgentDesk. The legacy Python bridge and ESP32 hardware firmware have been removed from this scope. The Windows Bridge is currently undergoing architecture migration into native .NET 8. The legacy repository (`AgentDesk`) remains active and preserved for physical device acceptance testing until full .NET Bridge validation.

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
   - `AgentDesk.Server`: Lightweight HTTP/WebSocket server listening on `127.0.0.1:8765`.
   - `AgentDesk.Desktop`: Windows System Tray app managing server lifecycle, Android launching, and ADB reverse tunneling.
   - `AgentDesk.Hook`: CLI bridge for receiving Codex / agent hook callbacks (`/api/v1/hooks/codex`).
   - `tests/AgentDesk.Core.Tests`: Unit test suite using xUnit.
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
```

---

## 🚀 Setup & Usage

### Prerequisites
- **.NET 8 SDK** (Required to compile and run `windows/` solution via `dotnet build` / `dotnet run`). *Note: Current environment requires installing the .NET 8 SDK.*
- **Android SDK & platform-tools** (`adb.exe` in `PATH` or `%LOCALAPPDATA%\Android\Sdk\platform-tools`).
- **Node.js (v18+) & npm** (for `web-ui` development and building assets).
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

#### 3. Build & Test .NET Windows Bridge (Requires .NET 8 SDK)
```powershell
dotnet restore windows/AgentDesk.sln
dotnet build windows/AgentDesk.sln --configuration Release --no-restore
dotnet test windows/AgentDesk.sln --configuration Release --no-build
```

#### 4. Run CLI Hook
```powershell
dotnet run --project windows/AgentDesk.Hook/AgentDesk.Hook.csproj
```

---

## 🔒 Security & Loopback Isolation

The Windows Bridge strictly binds to loopback (`127.0.0.1:8765`) and does not listen on external network interfaces (`0.0.0.0`). Communication from the physical Android device is securely routed over local USB debugging via `adb reverse tcp:8765 tcp:8765`.
