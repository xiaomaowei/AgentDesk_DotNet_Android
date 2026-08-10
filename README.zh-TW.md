# AgentDesk (.NET & Android)

[English](README.md) · [繁體中文](README.zh-TW.md)

AgentDesk 是一個 Agent 任務執行狀態監控面板，透過原生 Android 應用程式、H5 WebView 以及高效能的**純 .NET 8 Windows Bridge**，提供即時狀態顯示、互動式審核（Approvals）與桌面 Hook 整合。

> **專案狀態說明**：本 Repository 為 AgentDesk 的純 .NET 8 重構版本。舊版 Python Bridge 與 ESP32 硬體韌體已移除出此範疇。.NET 8 Windows Bridge 與 Samsung Galaxy S23 (SM-S9110) 實機端到端（E2E）驗收已全面通過。舊版 Repository (`AgentDesk`) 可繼續保留作為對照與備份，但不再需要作為未驗收之 Fallback 備用；本 Repository 即日起為持續開發的主庫。

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
   - `AgentDesk.Desktop`: Windows 工作列圖示（System Tray）應用程式，負責內嵌 Server 生命週期、Android 啟動與原生 ADB Reverse 管理。
   - `AgentDesk.Hook`: 接收 Codex 或其他 Agent 鉤子回呼的 CLI/Bridge 介面 (`/api/v1/hooks/codex`)。
   - `tests/`: Core、Server、Desktop 與 Hook 之單元與整合測試套件。
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
        ├── AgentDesk.Core.Tests/
        ├── AgentDesk.Server.Tests/
        ├── AgentDesk.Desktop.Tests/
        └── AgentDesk.Hook.Tests/
```

---

## 🚀 環境要求與使用說明

### 前置需求
- **.NET 8 SDK**：從原始碼建置或執行 `dotnet run` 需要 **.NET 8 SDK**。（GitHub Release 下載之 `AgentDesk-Windows-vX.Y.Z-win-x64.zip` 為免安裝自包含 Self-contained 包，目標機器無需安裝 .NET Runtime）。
- **Android SDK & platform-tools** (`adb.exe` 需在 `PATH`、`ANDROID_HOME`、`ANDROID_SDK_ROOT` 或 `%LOCALAPPDATA%\Android\Sdk\platform-tools`)。
- **Node.js (20.19+) & npm** (用於 `web-ui` 開發與產出靜態資源)。
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

#### 3. 建置、發布與執行 .NET Desktop 系統列應用程式與 Hook
點擊編譯後的 `AgentDesk.Desktop.exe` 可於背景系統列執行，不會出現 Console 視窗。

```powershell
# 發布 AgentDesk.Desktop 與 AgentDesk.Hook 至指定 artifacts 目錄
.\Publish-AgentDeskWindows.ps1

# 建置整個 Windows Solution
dotnet build windows/AgentDesk.sln -c Release

# 執行 Desktop 系統列應用程式
dotnet run --project windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj -c Release

# 執行全套測試
dotnet test windows/AgentDesk.sln --configuration Release --no-build
```

#### 4. 設定與驗證 Codex 專案 Hook
若要啟用 Codex 任務至 AgentDesk 的真實自動生命週期事件轉發：

1. **發布可執行檔**：執行 `.\Publish-AgentDeskWindows.ps1` 產出 `windows/artifacts/AgentDesk.Desktop-win-x64/AgentDesk.Desktop.exe` 與 `windows/artifacts/AgentDesk.Hook-win-x64/AgentDesk.Hook.exe`。
2. **啟動 Desktop 主程式**：執行 `AgentDesk.Desktop.exe`（或透過 `dotnet run --project windows/AgentDesk.Desktop/AgentDesk.Desktop.csproj`）。
3. **確認 ADB Reverse**：確認已針對連接之 Android 裝置開啟 `adb reverse tcp:8765 tcp:8765`。
4. **信任並核准專案 Hook**：當 Codex 提示時，核准並信任專案 `.codex/hooks.json`。
5. **確認已註冊之 Hook**：確認全數 7 個生命週期 Hook 皆已啟用（`SessionStart`、`SessionEnd`、`UserPromptSubmit`、`PreToolUse`、`PermissionRequest`、`PostToolUse`、`Stop`）。
6. **啟動真實任務**：啟動一個新的 Codex 任務以觸發自動生命週期事件轉發。

> **驗證狀態說明**：
> - **合成 Hook 驗證**：先前手動 CLI 呼叫（`AgentDesk.Hook.exe` 觸發合成 `UserPromptSubmit`）已作為早期低層級測試通過驗收。
> - **真實任務自動驗證 (2026-08-10)**：已於 Samsung Galaxy S23 (SM-S9110 / RFCW607NEMH) 實機完成真實自動生命週期轉發驗證。已配置全部 7 個 Hook，且於真實 Codex CLI v0.147.0 任務中成功自動觸發 5 個生命週期事件（`SessionStart`、`UserPromptSubmit`、`PreToolUse`、`PostToolUse`、`Stop`）。Server 狀態依序演進 `commentary:delivered` -> `tool:running` -> `tool:completed` -> `final:delivered` 並包含 Token 數，Android `SharedPreferences` 亦獨立觀察並確認手機可見欄位：`Prompt=true`、`Commentary=true`、`CompletedTool=true`、`Final=true`、`Tokens=true`（`PermissionRequest` 與自動 `SessionEnd` 已配置但本次未執行）。

---

## 📦 Release 與自動化發布流程

AgentDesk 使用 GitHub Actions (`.github/workflows/release.yml`) 進行自動化編譯、測試、打包與 Release 發布。每當推送符合根目錄 `VERSION` 檔案版本的 Tag（格式為 `vX.Y.Z`）時，系統會自動觸發此工作流程。

### GitHub Release 發布資產 (Assets)
每次觸發會產出三個發布檔案：
1. `AgentDesk-Windows-vX.Y.Z-win-x64.zip`: 免安裝自包含 (Self-contained) 64 位元 Windows 可執行檔 (`AgentDesk.Desktop.exe` 與 `AgentDesk.Hook.exe`)。
2. `AgentDesk-Android-vX.Y.Z.apk`: 已簽署之 Release 版 Android APK（內建完整 `web-ui` H5 靜態資源）。
3. `SHA256SUMS.txt`: SHA-256 校驗碼檔案，用於驗證資產完整性。

### 目標機器安裝與使用流程
1. **Windows 部署**:
   - 複製 (Clone) 本 Repository。
   - 自 GitHub Release 下載 `AgentDesk-Windows-vX.Y.Z-win-x64.zip`。
   - 將 ZIP 直接解壓縮至專案根目錄，這會自動建立：
     - `windows/artifacts/AgentDesk.Desktop-win-x64/`
     - `windows/artifacts/AgentDesk.Hook-win-x64/`
   - 啟動 `windows/artifacts/AgentDesk.Desktop-win-x64/AgentDesk.Desktop.exe`。
2. **Android 部署**:
   - 自同一個 GitHub Release 下載 `AgentDesk-Android-vX.Y.Z.apk`。
   - 安裝 APK 至 Android 裝置（例如執行 `adb install AgentDesk-Android-vX.Y.Z.apk`）。
   - 透過 USB 連接裝置並確認已開啟 ADB Reverse 通道 (`adb reverse tcp:8765 tcp:8765`)。

### 觸發發布指令
欲發布新版本時：
1. 更新根目錄 `VERSION`（例如 `0.1.0`）。
2. 建立並推送相對應之 Git Tag：
   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```
> **注意**：Workflow 會嚴格驗證 Tag 是否完全符合 `v` + `VERSION`。若版本不符，將在編譯發布前直接中斷並回報失敗。

### Android Release 簽署金鑰設定
請於 GitHub Repository Secrets（`Settings -> Secrets and variables -> Actions`）設定以下密鑰：
- `ANDROID_KEYSTORE_BASE64`: 簽署金鑰檔 (`.keystore` / `.jks`) 之 Base64 編碼字串。
- `ANDROID_KEYSTORE_PASSWORD`: Keystore 密碼。
- `ANDROID_KEY_ALIAS`: 金鑰別名 (Alias)。
- `ANDROID_KEY_PASSWORD`: 金鑰密碼。

> **注意**：全數四項簽署 Secrets 皆為必填。若缺少任何一項 Secrets，Release 工作流程將直接中斷並宣告失敗，絕不發布未簽署之 APK。

---

## 🔒 安全與 Loopback 隔離

Windows Bridge 嚴格綁定於 Loopback 介面 (`127.0.0.1:8765`)，不開放外部網路對外監聽 (`0.0.0.0`)。Android 實機透過 USB 連線與 `adb reverse tcp:8765 tcp:8765` 完成安全之本機通訊。
