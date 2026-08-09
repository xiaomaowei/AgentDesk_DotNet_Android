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
│  │   - Server Lifecycle & Restart Management             │  │
│  │   - ADB Reverse & Android Launch Controller           │  │
│  └───────────────────────────┬───────────────────────────┘  │
│                              │ Manages                      │
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
- **Security & Binding**:
  - **Loopback Isolation**: Strictly binds to `http://127.0.0.1:8765`.
  - Does NOT bind to external interfaces (`0.0.0.0`).
- **API Endpoints Compatibility Baseline**:
  - `GET /health`: Server health check endpoint returning `{ "status": "ok", "version": "..." }`.
  - `GET /api/v1/dashboard`: Retrieves current active dashboard state.
  - `GET /api/v1/events`: Server-Sent Events (SSE) or WebSocket endpoint for broadcasting real-time state updates to H5 UI.
  - `POST /api/v1/actions`: Handles action approval/rejection responses submitted by the user from the UI.
  - `POST /api/v1/hooks/codex`: Webhook endpoint for Codex agent lifecycle events.

### 3. `AgentDesk.Desktop` (Target: `net8.0-windows` with Windows Forms)
- **Purpose**: Windows System Tray application for user control.
- **Responsibilities**:
  - System Tray icon with status menu.
  - Controls `AgentDesk.Server` background process lifecycle (start, stop, restart).
  - Triggers ADB reverse tunnel setup (`Watch-AgentDeckAndroidReverse.ps1`) and launches Android App.
  - **State Protection**: When performing a server restart or state reset, the Desktop app must display a clear warning/confirmation prompt, explicitly notifying the user that in-memory state will be cleared.

### 4. `AgentDesk.Hook` (Target: `net8.0`)
- **Purpose**: Command-line entrypoint for agent hooks.
- **Responsibilities**:
  - Called by Codex CLI or system scripts to post raw agent lifecycle events to `AgentDesk.Server` at `/api/v1/hooks/codex`.

### 5. `tests/AgentDesk.Core.Tests` (Target: `net8.0`)
- **Purpose**: xUnit test suite for validating domain models, protocol serialization, and state mutations.

---

## 🚫 Out of Scope Architecture Elements

- **No ESP32 Hardware Firmware**: Hardware COM port / USB Serial bridge logic has been completely removed.
- **No Python Runtime**: Python virtual environment (`.venv`) and Python desktop bridge dependency have been eliminated.
