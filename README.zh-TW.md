# AgentDesk (.NET & Android)

AgentDesk 是一個 Agent 任務執行狀態監控面板，透過原生 Android 應用程式、H5 WebView 以及高效能的**純 .NET 8 Windows Bridge**，提供即時狀態顯示、互動式審核（Approvals）與桌面 Hook 整合。

> **專案狀態說明**：本 Repository 為 AgentDesk 的純 .NET 8 重構版本。舊版 Python Bridge 與 ESP32 硬體韌體已移除出此範疇。Windows Bridge 目前正進行 .NET 8 架構重寫。在實機全面驗收完成前，舊版 Repository (`AgentDesk`) 將繼續保留作為對照與驗收備用。

---

## 🏗 專案範圍與架構

AgentDesk (.NET & Android) 由以下四個核心層次組成：

```
┌─────────────────────────────────────────────────────────┐
│                     Android 實機 / 裝置                  │
│  ┌───────────────────────────────────────────────────┐  │
│  │   Android App (原生 Kotlin/Java 容器)             │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │   H5 混合介面 (React / Vite / TypeScript)   │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────┘
                           │ ADB Reverse Tunnel (tcp:8765)
                           ▼
┌─────────────────────────────────────────────────────────┐
│                 Windows 主機 (127.0.0.1:8765)            │
│  ┌───────────────────────────────────────────────────┐  │
│  │              AgentDesk.Desktop (WinForms 系統列)   │  │
│  └─────────────────────────┬─────────────────────────┘  │
│                            │ 控制與管理                  │
│  ┌─────────────────────────▼─────────────────────────┐  │
│  │              AgentDesk.Server (ASP.NET Core)      │  │
│  │  - Loopback API (http://127.0.0.1:8765)           │  │
│  │  - 協定處理常式 (/api/v1/*)                       │  │
│  └─────────────────────────▲─────────────────────────┘  │
│                            │ 呼叫                        │  │
│  ┌─────────────────────────┴─────────────────────────┐  │
│  │  AgentDesk.Hook (CLI / 系統整合工具)             │  │
│  │  AgentDesk.Core (共享領域邏輯與模型)              │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### 核心模組說明
1. **`windows/` (.NET 8 Windows Bridge)**:
   - `AgentDesk.Core`: 領域模型、協定 Schema 與狀態管理。
   - `AgentDesk.Server`: 輕量級 HTTP API Server，僅監聽 `127.0.0.1:8765`。
   - `AgentDesk.Desktop`: Windows 工作列圖示（System Tray）應用程式，負責 Server 生命週期、Android 啟動與 ADB Reverse 管理。
   - `AgentDesk.Hook`: 接收 Codex 或其他 Agent 鉤子回呼的 CLI/Bridge 介面 (`/api/v1/hooks/codex`)。
   - `tests/AgentDesk.Core.Tests`: 基於 xUnit 的單元測試套件。
2. **`android-app/`**: 原生 Android 應用程式，承載 WebView 並透過 ADB Reverse HTTP API 與 Windows Bridge 通訊。
3. **`web-ui/`**: 嵌入於 Android WebView 的 React H5 控制面板，展示當前 Turn、步驟進度、Diff 比對與 Action 操作審核。
4. **`protocol/`**: Protocol v1 協定規格、JSON Schema 與 Payload 範例。

---

## 📁 目錄結構

```
.
├── AGENTS.md                   # Agent 協作與開發規範
├── Install-AgentDeckAndroid.ps1 # 驗證 H5、編譯 APK、安裝並啟動的腳本
├── Start-AgentDeckAndroid.ps1   # 管理 ADB Reverse 通道並啟動 Android App 的腳本
├── LICENSE
├── VERSION
├── android-app/                # 原生 Android Gradle 專案
├── docs/                       # 架構、遷移、Android 與協定文件
│   ├── android.md
│   ├── architecture.md
│   ├── migration.md
│   └── protocol.md
├── protocol/                   # 協定 Schema 與範例 Json
├── scripts/
│   └── Watch-AgentDeckAndroidReverse.ps1 # ADB Reverse 自動修復監看器
├── web-ui/                     # React/TypeScript H5 前端原始碼
└── windows/                    # .NET 8 Windows Bridge 專案與 Solution
    ├── Directory.Build.props
    ├── README.md
    ├── AgentDesk.Core/
    ├── AgentDesk.Server/
    ├── AgentDesk.Desktop/
    ├── AgentDesk.Hook/
    └── tests/
```

---

## 🚀 環境要求與使用說明

### 前置需求
- **.NET 8 SDK** (建置與執行 `windows/` 專案需安裝 .NET 8 SDK，目前環境僅有 .NET Runtime)。
- **Android SDK & platform-tools** (`adb.exe` 需在 `PATH` 中，或位於 `%LOCALAPPDATA%\Android\Sdk\platform-tools`)。
- **Node.js (v18+) & npm** (用於 `web-ui` 開發與產出靜態資源)。
- **JDK 17+** (用於 Android APK 編譯)。

### 常用指令

#### 1. 驗證並安裝 Android App
```powershell
.\Install-AgentDeckAndroid.ps1
```
此腳本會驗證 `web-ui`（lint、typecheck、test、build）、透過 Gradle 編譯 Android Debug APK、安裝至 Android 裝置、建立 ADB Reverse Port Forwarding 並啟動 App。

#### 2. 啟動 Android App 與通道
```powershell
.\Start-AgentDeckAndroid.ps1
```
維持 `adb reverse tcp:8765 tcp:8765` 監聽，啟動背景 Watcher，開啟 Android 應用程式，並檢查 `http://127.0.0.1:8765/health`。若 .NET Server 尚未啟動，將顯示提示訊息。

#### 3. 建置與測試 .NET Windows Bridge（需 .NET 8 SDK）
```powershell
cd windows
dotnet build
dotnet test
```

---

## 🔒 安全與 Loopback 隔離

Windows Bridge 嚴格綁定於 Loopback 介面 (`127.0.0.1:8765`)，不開放外部網路對外監聽 (`0.0.0.0`)。Android 實機透過 USB 連線與 `adb reverse tcp:8765 tcp:8765` 完成安全之本機通訊。
