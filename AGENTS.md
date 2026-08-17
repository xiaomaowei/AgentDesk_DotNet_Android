# AgentDesk Project Rules

全域 `C:\Users\020183\.codex\AGENTS.md` 定義共同回覆、證據與最小變更規則。本檔只保留 AgentDesk 的環境與驗收差異。

## Hybrid routing

- Codex 負責需求、架構、拆解、Issue/Direct Task、Review 與 acceptance decision。
- agy 負責 How、Code、Build、Test；可用性、模型與 dispatch 透過 PowerShell 7 (`pwsh`) 即時確認。
- agy 使用目前可用的 Gemini 模型；Claude disabled 時維持停用。只有確認 agy 不可用才使用既有 Codex fallback。
- GitHub implementation Issue 必須使用現有 repository label；沒有合適 label 時回報並停止無標籤 handoff。

## Runtime boundaries

- 預設 Desktop/Bridge Health 位址為 `http://127.0.0.1:8765/health`；先辨識目前程序與工作樹，再決定是否重啟。
- 保留現有 Desktop、Hook、Android、Web UI 與未提交修改；修改一個功能時維持其餘產品面不變。
- 公司專用 publish、簽名與 machine-local 設定使用獨立且明確 ignored 的變體，不把公司腳本、憑證、密鑰或 artifacts 推送到 GitHub。

## Acceptance

- Android UI/APK：完成 assemble 後安裝到目前連線的手機，連接目前 source Bridge，檢查實機畫面並回報截圖絕對路徑。
- Desktop/Hook：除測試外，檢查 publish artifact、Authenticode、SHA-256、Zone.Identifier、程序路徑與 `/health`；使用 `$windows-release-acceptance`。
- Desktop 關閉時 Hook 必須停止執行；使用 health gate 並檢查沒有新程序產生。
- 對話、額度、步驟或完成狀態變更，驗收實際 API/Dashboard 與 Stop/完成後狀態，不只驗證事件傳輸中的畫面。
- 任何未能在本機驗證的部分標示為 target-environment acceptance，不宣稱完整完成。

## Required report

使用 `$acceptance-evidence` 回報：Status、Summary、Changed、Validation、Live/Artifact Evidence、Issues、Next Acceptance Step。
