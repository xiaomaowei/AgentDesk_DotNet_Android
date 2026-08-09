# AgentDesk Windows Solution (.NET 8)

This directory contains the .NET 8 Windows Bridge solution and projects for AgentDesk.

## Projects

- **`AgentDesk.Core`** (`net8.0`): Shared domain models, protocol v1 contracts, and state management.
- **`AgentDesk.Server`** (`net8.0`): ASP.NET Core loopback HTTP server (`127.0.0.1:8765`).
- **`AgentDesk.Desktop`** (`net8.0-windows`): WinForms System Tray application (pending).
- **`AgentDesk.Hook`** (`net8.0`): CLI utility for sending agent lifecycle events to the server.
- **`tests/AgentDesk.Core.Tests`** (`net8.0`): Core unit test suite.
- **`tests/AgentDesk.Server.Tests`** (`net8.0`): Server integration test suite.
- **`tests/AgentDesk.Hook.Tests`** (`net8.0`): Hook CLI unit test suite.

## Commands

Requires **.NET 8.0 SDK**.

```powershell
# Restore dependencies
dotnet restore windows/AgentDesk.sln

# Build solution
dotnet build windows/AgentDesk.sln --configuration Release --no-restore

# Run test suites
dotnet test windows/AgentDesk.sln --configuration Release --no-build

# Run ASP.NET Core Server
dotnet run --project windows/AgentDesk.Server/AgentDesk.Server.csproj

# Run CLI Hook
dotnet run --project windows/AgentDesk.Hook/AgentDesk.Hook.csproj
```
