# AgentDesk 交接文件 (Handoff Document)

本文件用於在公司電腦上繼續進行 AgentDesk_DotNet_Android 專案開發與測試。

## Current status

- **Repository**: AgentDesk_DotNet_Android
- **Branch**: main
- **Current Commit**: 4f29aa4
- **Git Status**: `origin/main` 已同步（synchronized），在建立本檔案前工作區為乾淨狀態（working tree clean）。

## Root cause

- **原始問題**：Android 端僅顯示 Prompt，無法正常接收與呈現後續的動態事件。
- **根本原因**：新的 .NET 儲存庫未追蹤 `.codex/hooks.json`，且 Codex 自動生命週期事件未連接至 .NET Hook。

## Completed work

- **Hook 設定**：追蹤 `.codex/hooks.json`，確保 Codex 自動觸發生命週期事件。
- **建置腳本**：新增 `Publish-AgentDeskWindows.ps1` 腳本，自動化編譯與發布 Windows 元件。
- **解析與數據處置**：
  - `CodexTranslator` 解說文字去重（commentary deduplication）與 Token 提取（token extraction）。
  - 設定上限的 `TranscriptParser`（Bounded 256 KiB）。
  - 在執行超過 20 個 Tool 後仍保留最終結果與解說文字（retaining final/commentary after 20 tools）。
- **測試與文件**：新增 Core 與 Hook 單元測試，並更新 README 與相關文件。
- **Android 原生架構維護**：Android 原生所有權維持不變，由 `AgentDeckService` 負責 SSE、快取與通知；H5 透過 `WebMessageChannel` 與原生層進行通訊。

## Verification

- **.NET 測試**：106 個單元測試全數通過（106 tests passed）。
- **H5 前端**：Typecheck、Lint、53 個單元測試及生產建置皆順利通過。
- **Android 測試**：`testDebugUnitTest`、`assembleDebug` 與 `lintDebug` 全數通過。
- **Git 檢查**：`git diff --check` 通過，無格式或空白字元異常。
- **實機驗證（Physical Validation）**：
  - **裝置資訊**：Samsung S23 SM-S9110 (Serial: RFCW607NEMH)。
  - **安裝與網路**：`adb install -r` 成功，`adb reverse tcp:8765 tcp:8765` 完成通訊連接。
  - **Codex CLI 整合**：搭配真實 Codex CLI v0.147.0 執行任務，自動觸發 `SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `Stop`。
  - **即時觀察**：Server 與 S23 實機同步觀察到 Prompt、Commentary、Tool Running、Tool Completed、Final 以及 Token Delivery 等完整事件與數據。

## Company setup

在公司電腦複製與設定專案的執行指令如下：

1. **複製儲存庫 (Git Clone)**
   ```powershell
   git clone <repository_url>
   cd AgentDesk_DotNet_Android
   ```

2. **安裝 Web UI 依賴套件**
   ```powershell
   cd web-ui
   npm ci
   cd ..
   ```

3. **發布 Windows 執行檔**
   ```powershell
   .\Publish-AgentDeskWindows.ps1
   ```

4. **啟動 Desktop 背景服務**
   ```powershell
   Start-Process -FilePath "windows\artifacts\AgentDesk.Desktop-win-x64\AgentDesk.Desktop.exe" -WindowStyle Hidden
   ```

5. **執行健康檢查 (Health Check)**
   ```powershell
   Invoke-RestMethod -Uri "http://localhost:8765/health"
   ```

6. **設定 Android 實機與安裝 App**
   ```powershell
   .\Install-AgentDeckAndroid.ps1
   # 或手動執行:
   # adb reverse tcp:8765 tcp:8765
   # adb install -r android-app/app/build/outputs/apk/debug/app-debug.apk
   ```

7. **信任與核准 Hook 設定**
   - 確保 Codex 設定信任並核准專案中的 `.codex/hooks.json`。

8. **啟動 Codex 任務驗證**
   - 啟動新的 Codex CLI 任務，觀察 Android 裝置與 Desktop 上事件接收狀況。

## Known limitations

- **未實機觸發之事件**：`PermissionRequest` 與自動 `SessionEnd` 已完成配置與程式碼處理，但在本次驗證中未進行實機觸發。
- **構建產物未追蹤**：產物檔案屬於構建輸出，未納入 Git 追蹤，必須在公司電腦上重新建置：
  - `windows/artifacts/AgentDesk.Desktop-win-x64/AgentDesk.Desktop.exe`
  - `windows/artifacts/AgentDesk.Hook-win-x64/AgentDesk.Hook.exe`
  - `android-app/app/build/outputs/apk/debug/app-debug.apk`
  - (版本號 version 0.1.0)

## Next steps

1. 在公司電腦上執行 `git clone` 與環境編譯 (重新產出 artifacts)。
2. 啟動 `AgentDesk.Desktop.exe` 並連接 Android 實機進行 Port Reverse (`adb reverse tcp:8765 tcp:8765`)。
3. 執行包含權限請求 (`PermissionRequest`) 的 Codex 任務以進一步驗證完整邊界狀況。
