# AgentDesk Migration Guide (.NET & Android)

This document tracks the transition from the legacy Python-based AgentDesk repository to the pure **.NET 8 Windows Bridge & Android** architecture.

---

## 🎯 Migration Goals

1. **Replace Python Bridge**: Transition the desktop host bridge from Python (`agentdeck_bridge`) to a high-performance native .NET 8 solution (`AgentDesk.Server`, `AgentDesk.Desktop`, `AgentDesk.Hook`, `AgentDesk.Core`).
2. **Streamline Repository Scope**: Focus strictly on Android App, H5 Web UI, Protocol specs, and .NET Windows Bridge. Remove legacy ESP32 hardware firmware and COM/USB Serial adapters.
3. **Maintain Protocol & App Compatibility**: Ensure full backward compatibility with the Android App (`com.agentdeck.mobile` / `.MainActivity`), H5 UI endpoints (`/health`, `/api/v1/*`), and ADB reverse tunnel configuration (`tcp:8765 tcp:8765`).

---

## 📋 Migration Status & Checklist

### Phase 1: Source Extraction & Project Structure Establishment
- [x] Copy git-tracked Android App (`android-app/**`) source code.
- [x] Copy git-tracked H5 Web UI (`web-ui/**`) source code.
- [x] Copy Protocol specification (`protocol/**`) and `docs/protocol.md`.
- [x] Copy root automation scripts (`Install-AgentDeckAndroid.ps1`, `Start-AgentDeckAndroid.ps1`, `scripts/Watch-AgentDeckAndroidReverse.ps1`).
- [x] Copy project collaboration rules (`AGENTS.md`) as-is.
- [x] Create comprehensive `.gitignore` covering .NET (`bin/`, `obj/`, `artifacts/`), Android, H5 (`node_modules/`, `dist/`), and IDE caches while preserving lock files.
- [x] Establish minimal .NET solution structure under `windows/`:
  - `Directory.Build.props`
  - `windows/README.md`
  - `windows/AgentDesk.Core/AgentDesk.Core.csproj`
  - `windows/AgentDesk.Server/AgentDesk.Server.csproj`
  - `windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj`
  - `windows/AgentDesk.Hook/AgentDesk.Hook.csproj`
  - `windows/tests/AgentDesk.Core.Tests/AgentDesk.Core.Tests.csproj`

### Phase 2: Script Adaptation & Transition Safety
- [x] Update `Start-AgentDeckAndroid.ps1` to remove Python venv invocation and transition to managing ADB reverse tunnel, reverse watcher, and Android App launch, while checking `http://127.0.0.1:8765/health`.
- [x] Update `Install-AgentDeckAndroid.ps1` to retain H5 validation, Android build, ADB reverse, and app launch.
- [x] Update `docs/android.md` to reflect .NET Bridge architecture and remove legacy Python venv instructions.

### Phase 3: .NET Bridge Implementation & Physical Device Acceptance
- [x] Implement `AgentDesk.Core` domain models, protocol serializer, and in-memory `StateStore`.
- [x] Implement `AgentDesk.Server` ASP.NET Core loopback listener on `127.0.0.1:8765` with endpoints (`/health`, `/api/v1/dashboard`, `/api/v1/events`, `/api/v1/actions`, `/api/v1/hooks/codex`).
- [x] Implement `AgentDesk.Desktop` WinForms System Tray app with embedded server lifecycle control, restart state clear prompts, and native ADB reverse triggers.
- [x] Implement `AgentDesk.Hook` CLI executable.
- [x] Implement `AgentDesk.Core.Tests`, `AgentDesk.Server.Tests`, `AgentDesk.Desktop.Tests`, and `AgentDesk.Hook.Tests` test coverage.
- [x] Conduct end-to-end acceptance testing on physical Android device (Samsung Galaxy S23, SM-S9110) with .NET Windows Bridge.

---

## 🔍 Validation Checklist for Migration

| Category | Verification Step | Target / Tool | Expected Result | Status |
| :--- | :--- | :--- | :--- | :--- |
| **Cleanliness** | **Clean Repository** | `.gitignore` & untracked file check | Generated outputs (`node_modules`, `.venv`, `.pio`, `build`, `bin`, `obj`) untracked & ignored | Passed |
| **Automated / Build** | **Script Syntax** | PowerShell AST parser (`.ps1`, `scripts/*.ps1`) | Zero syntax errors across root and watcher scripts | Passed |
| **Automated / Build** | **H5 Web UI** | `npm --prefix web-ui` (lint, typecheck, test, build) | 53/53 tests, lint/typecheck clean, production build pass | Passed |
| **Automated / Build** | **Android Build** | `./gradlew` (testDebugUnitTest, assembleDebug, lintDebug) | Build & unit tests pass (build-level validation only) | Passed |
| **Automated / Build** | **.NET Solution** | `dotnet` (Release build, tests, format verify) | 0 warnings/errors, 99/99 solution tests pass, format verified | Passed |
| **Loopback Smoke** | **Server & Hook Smoke** | Loopback HTTP (`127.0.0.1:8765`) & Hook CLI | `/health`, `/api/v1/dashboard` envelope & Hook CLI->Server pass | Passed |
| **Desktop App** | **Desktop Tray UI** | `AgentDesk.Desktop` WinForms system tray | System tray app lifecycle, restart/clear prompt & ADB triggers | Implementation + automated tests Passed |
| **Physical Device** | **S23 Acceptance** | Physical Android (SM-S9110 / S23) via ADB reverse | E2E physical acceptance passed (APK install, auto reverse recovery, tray launch, Hook-to-H5 rendering & restart safety) | Passed |
