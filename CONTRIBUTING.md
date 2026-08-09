# Contributing to AgentDesk (.NET & Android)

Thank you for contributing! This document outlines the development workflow and verification requirements for **AgentDesk (.NET & Android)**.

---

## 📋 Repository Architecture & Scope

This repository strictly contains:
1. **Android App (`android-app/`)**: Native Kotlin/Java WebView host container.
2. **H5 Web UI (`web-ui/`)**: React / TypeScript / Vite dashboard interface.
3. **Windows Bridge (`windows/`)**: .NET 8 solution (`AgentDesk.Core`, `AgentDesk.Server`, `AgentDesk.Desktop`, `AgentDesk.Hook`, `tests/`).
4. **Protocol Specifications (`protocol/`)**: JSON schemas and example event payloads.

> **Note**: Legacy components such as Python `desktop-bridge` and ESP32 hardware firmware are **out of scope** for this repository and must not be introduced or submitted.

---

## 🛠 Verification & Testing Workflow

Before submitting changes, all components modified must pass their respective verification pipelines.

### 1. H5 Web UI (`web-ui/`)
All H5 frontend changes must pass TypeScript checks, linting, unit tests, and production build verification.

```powershell
# Navigate to web-ui
cd web-ui

# Install dependencies (only when package.json changes)
npm ci

# Run Linter
npm run lint

# Run Typecheck
npm run typecheck

# Run Unit Tests
npm test -- --run

# Run Production Build
npm run build
```

### 2. Android App (`android-app/`)
Android code changes must compile cleanly, pass unit tests, and build the APK.

```powershell
# Navigate to android-app
cd android-app

# Run Unit Tests, assemble Debug APK, and run Android Lint
.\gradlew.bat testDebugUnitTest assembleDebug lintDebug
```

You can also run the full end-to-end installation script on a connected Android device:
```powershell
.\Install-AgentDeckAndroid.ps1
```

### 3. .NET Windows Bridge (`windows/`)
*.NET 8 SDK is required for compiling and testing the Windows Bridge components.*

```powershell
# Navigate to windows
cd windows

# Build solution
dotnet build --configuration Release

# Run unit test suite
dotnet test --configuration Release
```

---

## 📏 Code Style & Standards

- **Agent Guidelines**: Always adhere to the project rules outlined in [`AGENTS.md`](./AGENTS.md).
- **Loopback Isolation**: Server code must strictly bind to `127.0.0.1` and never listen on all network interfaces (`0.0.0.0`).
- **No Direct Git Operations**: Do not execute unapproved Git actions; follow the Codex / Implementation Agent workflow.
