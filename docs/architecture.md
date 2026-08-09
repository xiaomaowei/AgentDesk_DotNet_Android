# AgentDesk (.NET & Android) Architecture

This document describes the target architecture for the **pure .NET 8 Windows Bridge** and its integration with the Android App and H5 Web UI.

---

## 🏗 High-Level System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Android Mobile Device                    │
│  ┌───────────────────────────────────────────────────────┐  │
│  │   Android App (Native Kotlin/Java Container)          │  │
│  │  ┌─────────────────────────────────────────────────┐  │  │
│  │  │   H5 Web UI (React / Vite / TypeScript)         │  │  │
│  │  └─────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────┘  │
└──────────────────────────────┬──────────────────────────────┘
                               │ ADB Reverse Tunnel (tcp:8765)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                 Windows Host (127.0.0.1:8765)               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │           AgentDesk.Desktop (WinForms System Tray)    │  │
│  │   - Tray Icon & Context Menu                          │  │
│  │   - Embedded Server Lifecycle & Restart Control       │  │
│  │   - Native ADB Reverse & Android Launch Controller    │  │
│  └───────────────────────────┬───────────────────────────┘  │
│                              │ Embeds & Controls            │
│  ┌───────────────────────────▼───────────────────────────┐  │
│  │           AgentDesk.Server (ASP.NET Core)             │  │
│  │   - Loopback HTTP Listener (http://127.0.0.1:8765)    │  │
│  │   - Endpoint Routing & Event Stream Broadcasting       │  │
│  └───────────────────────────▲───────────────────────────┘  │
│                              │ Invokes                      │
│  ┌───────────────────────────┴───────────────────────────┐  │
│  │   AgentDesk.Hook (CLI / Agent Integration)            │  │
│  │   AgentDesk.Core (Domain Models, State, Protocol v1)  │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 System Components

### 1. `AgentDesk.Core` (Target: `net8.0`)
- **Purpose**: Core class library containing shared domain models, AgentDesk v1 protocol data contracts, state management, and validation logic.
- **Responsibilities**:
  - In-memory dashboard state holder (`DashboardState`).
  - Serialization / deserialization of protocol v1 payloads (`agentdeck-v1.schema.json`).
  - Turn step parsing, token calculation, redaction helpers, and action validation.

### 2. `AgentDesk.Server` (Target: `net8.0`)
- **Purpose**: High-performance ASP.NET Core minimal API server.
- **Embeddable Host Architecture**:
  - Exposes `AgentDeskServer.Build(string[] args, string? urlOverride = null)` returning `WebApplication`.
  - Can be run standalone via `Program.cs` or hosted in-process by `AgentDesk.Desktop`.
- **Security & Binding**:
  - **Loopback Isolation**: Strictly binds to `http://127.0.0.1:8765` by default.
  - Does NOT bind to external interfaces (`0.0.0.0`).
- **API Endpoints Baseline**:
  - `GET /health`: Server health check endpoint returning `{ "status": "ok", ... }`.
  - `GET /api/v1/dashboard`: Retrieves current active dashboard state.
  - `GET /api/v1/events`: Server-Sent Events (SSE) stream for broadcasting real-time state updates to H5 UI.
  - `POST /api/v1/actions`: Handles action approval/rejection responses submitted by the user from the UI.
  - `POST /api/v1/hooks/codex`: Webhook endpoint for Codex agent lifecycle events.

### 3. `AgentDesk.Desktop` (Target: `net8.0-windows` with Windows Forms)
- **Purpose**: Windows System Tray application for user control and native runtime management.
- **Responsibilities**:
  - **No-Window Tray UI**: Runs in system tray via `NotifyIcon` without showing a console window or main UI form.
  - **In-Process Server Control**: Manages embedded `AgentDesk.Server` lifecycle (`StartAsync`, `StopAsync`, `RestartAsync`). Recreates the web host and DI singletons on restart to clear all session and approval state.
  - **Single Instance Enforcement**: Enforces a single running instance via a named mutex (`Global\AgentDesk.Desktop.SingleInstance`).
  - **Native ADB Manager**: Discovers `adb.exe` across candidate paths, parses `adb devices -l`, maintains `adb reverse tcp:8765 tcp:8765`, and triggers explicit activity launches (`com.agentdeck.mobile/.MainActivity`). Runs background periodic status checks (~every 2s).
  - **State Protection**: Displays explicit confirmation dialogs before restarting the server or resetting session state.

### 4. `AgentDesk.Hook` (Target: `net8.0`)
- **Purpose**: Command-line entrypoint for agent hooks.
- **Responsibilities**:
  - Called by Codex CLI or system scripts to post raw agent lifecycle events to `AgentDesk.Server` at `/api/v1/hooks/codex`.

### 5. `tests/` Test Suites (Target: `net8.0` / `net8.0-windows`)
- **`AgentDesk.Core.Tests`**: Unit tests for domain models, protocol serialization, and state mutations.
- **`AgentDesk.Server.Tests`**: Integration tests using `WebApplicationFactory<Program>` to test HTTP endpoints and approval workflows.
- **`AgentDesk.Desktop.Tests`**: Unit tests for ADB device parsing, reverse tunnel detection, command runner plans, status formatting, and embedded server dynamic port lifecycle.
- **`AgentDesk.Hook.Tests`**: Unit tests for CLI hook payload translation and execution.

---

## 🚫 Out of Scope Architecture Elements

- **No ESP32 Hardware Firmware**: Hardware COM port / USB Serial bridge logic has been completely removed.
- **No Python Runtime**: Python virtual environment (`.venv`) and Python desktop bridge dependency have been eliminated.
