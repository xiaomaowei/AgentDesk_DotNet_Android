# AgentDesk Windows Solution (.NET 8)

This directory contains the .NET 8 Windows Bridge solution and projects for AgentDesk.

## Projects

- **`AgentDesk.Core`** (`net8.0`): Shared domain models, protocol v1 contracts, and state management.
- **`AgentDesk.Server`** (`net8.0`): ASP.NET Core loopback HTTP server (`127.0.0.1:8765`).
- **`AgentDesk.Desktop`** (`net8.0-windows`): WinForms System Tray application managing server lifecycle, Android app launching, and native ADB reverse tunneling (`tcp:8765 tcp:8765`).
- **`AgentDesk.Hook`** (`net8.0`): CLI utility for sending agent lifecycle events to the server.
- **`tests/AgentDesk.Core.Tests`** (`net8.0`): Core unit test suite.
- **`tests/AgentDesk.Server.Tests`** (`net8.0`): Server integration test suite.
- **`tests/AgentDesk.Desktop.Tests`** (`net8.0-windows`): Desktop tray app & ADB manager unit test suite.
- **`tests/AgentDesk.Hook.Tests`** (`net8.0`): Hook CLI unit test suite.

## Requirements & Environment

- **Building from source**: .NET 8.0 SDK.
- **Running published framework-dependent executable (`AgentDesk.Desktop.exe`)**: .NET 8 Desktop Runtime (`windowsdesktop-runtime-8.0`).
- **Android SDK & platform-tools** (optional, for device control): `adb.exe` in `PATH`, `ANDROID_HOME`, `ANDROID_SDK_ROOT`, or `%LOCALAPPDATA%\Android\Sdk\platform-tools`.

## System Tray Features

Double-clicking `AgentDesk.Desktop.exe` runs the application silently in the Windows Notification Area (System Tray) without showing a console window or main UI form.
- **Status Display**: Disabled context menu header showing real-time embedded server and ADB connection state.
- **Restart Server...**: Recreates the embedded ASP.NET Core host and DI singletons after user confirmation, clearing in-memory session/approval state.
- **Connect & Launch Android**: Automatically starts the ADB daemon, configures ADB reverse port forwarding (`tcp:8765 tcp:8765`), and launches `com.agentdeck.mobile/.MainActivity`.
- **Exit**: Performs a bounded graceful shutdown of ADB monitoring, stops and disposes the embedded server host, releases single-instance mutex, and cleans up tray resources.

## Commands

```powershell
# Restore dependencies
dotnet restore windows/AgentDesk.sln

# Build solution
dotnet build windows/AgentDesk.sln --configuration Release --no-restore

# Run test suites
dotnet test windows/AgentDesk.sln --configuration Release --no-build

# Run System Tray App
dotnet run --project windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj -c Release

# Publish framework-dependent x64 Desktop executable
dotnet publish windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj -c Release -r win-x64 --self-contained false -o windows/artifacts/AgentDesk.Desktop-win-x64

# Run standalone ASP.NET Core Server
dotnet run --project windows/AgentDesk.Server/AgentDesk.Server.csproj -c Release

# Run CLI Hook
dotnet run --project windows/AgentDesk.Hook/AgentDesk.Hook.csproj -c Release
```
