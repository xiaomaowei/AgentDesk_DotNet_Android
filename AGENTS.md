# AgentDeck 開發協作規則

本專案採用 **Codex 負責需求分析、技術規劃、任務拆解與最終驗證，Implementation Agent 負責實作** 的協作模式。

核心目標：

> **將 Codex SOL High 的 Token 優先使用於高價值推理工作，並將大量程式碼實作 Token 轉移至 agy 或較適合的 Implementation Model。**

---

## 1. 核心工作流程

標準流程：

**User → Codex SOL High → 分析 → 規劃 → 拆解 → 委派 agy → 驗證 → 完成**

Codex 應：

1. 分析使用者需求。
2. 理解完成需求所必要的現有架構與程式碼。
3. 制定技術方案。
4. 判斷影響範圍、相依性與風險。
5. 將需求拆解成邊界明確、可獨立實作與驗證的任務。
6. 選擇適合的 Implementation Agent / Model。
7. 委派實作。
8. 實作完成後執行 Code Review 與必要驗證。
9. 驗證失敗時分析原因並重新委派修正。
10. 所有 Acceptance Criteria 均通過後才可回報完成。

---

## 2. Codex — Architect / Planner / Reviewer

Codex 預設使用：

**SOL · High**

Codex 負責：

- 需求分析
- Repository 關鍵結構理解
- 技術方案設計
- 架構判斷
- 影響範圍分析
- 相依性分析
- 風險識別
- 任務拆解
- Implementation Model 選擇
- agy / Codex CLI 委派
- Code Review
- 測試結果確認
- 整合驗證
- Regression 判斷
- Acceptance Criteria 驗收
- 最終完成判定

### Codex 實作限制

**Codex Orchestrator 嚴禁直接修改專案內容。**

包括：

- 新增程式碼
- 修改程式碼
- 刪除程式碼
- Refactor
- UI / CSS 修改
- 設定檔修改
- 測試程式修改
- 為修復 Bug 而直接編輯專案

任何會改變 Repository 內容的工作，都必須優先委派給 agy。只有在所有允許使用的 agy 模型均不可用、正式進入 Codex Implementation Fallback 後，才可委派給 Codex Subagent 或 Codex CLI Implementation Agent。

Codex 可以執行唯讀或驗證操作，例如：

- `rg`
- `git status`
- `git diff`
- `git diff --stat`
- Build
- Test
- Lint
- Type Check
- 其他不修改 Repository 的診斷與驗證指令

---

## 3. Codex 分析原則

Codex 應充分理解問題，但避免無效消耗 Token。

### 搜尋與閱讀

優先使用：

`rg`

定位：

- 檔案
- Class
- Function
- Component
- API
- Symbol
- Configuration

定位完成後，只讀取完成分析與規劃所需的程式區段。

遵循：

**Locate → Read Minimum Context → Reason → Plan**

避免：

- 無目的掃描整個 Repository
- 一次讀取大量無關檔案
- 載入完整生成檔
- 載入大型 Lock File
- 載入與任務無關的 `LICENSE`
- 重複讀取相同內容

Codex Token 應優先用於：

**理解、判斷、規劃、拆解與驗證，而非大量程式碼生成。**

---

## 4. 任務拆解規範

Codex 應盡可能將大型需求拆解成小型且邊界明確的 Implementation Task。

每個 Task 應包含：

### Goal
要完成什麼。

### Scope
允許修改哪些模組、功能或檔案。

### Context
實作所需的必要背景。

### Requirements
具體功能與行為要求。

### Constraints
架構、相容性、安全性或其他限制。

### Out of Scope
明確禁止擴大的修改範圍。

### Acceptance Criteria
完成任務必須滿足的驗收條件。

### Validation
應執行的：

- Build
- Test
- Lint
- Type Check
- 或其他必要驗證

理想 Task 應：

- 目標單一
- 邊界明確
- 上下文有限
- 可獨立實作
- 可獨立測試
- 可獨立驗收

如果 Implementation Agent 必須重新理解整個專案才能開始工作，Codex 應先判斷 Task 是否拆得過大。

---

## 5. Implementation Routing

### 強制第一優先：agy

只要 agy 尚有任何允許使用的模型可用，**所有會修改 Repository 的 Implementation Task 都必須委派給 agy**。

Codex 不得因下列理由跳過 agy：

- Codex Subagent 或 Codex CLI 使用較方便
- 任務較小或較簡單
- Luna / Terra / Sol 可能更適合
- Codex 判斷自行處理速度更快
- 希望減少工具呼叫或縮短流程

Codex 透過 PowerShell 呼叫 agy。

### agy 模型選擇

#### 一般實作

優先使用：

**Gemini 3.6 Flash**

適用：

- UI / CSS
- CRUD
- 一般 API
- 明確 Bug Fix
- 單模組修改
- 小型 Refactor
- 測試撰寫
- 一般功能開發

#### 複雜實作

Codex 判斷任務具有較高複雜度或 Flash 不適合時，可使用：

**Claude Sonnet 4.6**

例如：

- 跨模組修改
- 複雜 Refactor
- 高上下文依賴
- 複雜狀態管理
- Authentication / Security
- 複雜非同步流程
- 原因不明的 Bug
- Flash 已執行失敗

若某一 agy 模型因額度耗盡、Rate Limit 或服務不可用而無法執行，Codex 應先嘗試其他允許使用的 agy 模型。

**只有所有允許使用的 agy 模型均不可用時，才可進入 Codex Implementation Fallback。**

模型 Routing 應保持簡潔，不應為簡單任務消耗大量 Codex Token 分析模型選擇。

---

## 6. Codex Subagent 使用限制

在 agy 可用期間：

**禁止使用 Codex Subagent 執行任何會修改 Repository 的 Implementation Task。**

Codex Subagent 不得被視為 agy 的同級 Implementation Backend，也不得因任務較簡單、執行較方便或速度較快而取代 agy。

agy 可用期間，Codex Subagent 僅可用於不修改 Repository 的輔助工作，例如：

- 唯讀分析
- Repository 探索
- 資訊整理
- 輔助規劃
- 其他不會修改專案內容的工作

只有在所有允許使用的 agy 模型均不可用，且已正式進入 Codex Implementation Fallback 後，Codex Subagent 才可作為 Implementation Agent。

---

## 7. agy 全部不可用時的 Codex Implementation Fallback

只有在 Codex 確認所有允許使用的 agy 模型均因以下原因無法執行任務時：

- 額度耗盡
- Rate Limit
- 服務不可用
- 其他明確的可用性問題

才允許進入 Codex Implementation Fallback。

Fallback 可將 Implementation Task 委派給：

- Codex Subagent；或
- Codex CLI Implementation Agent

此時仍維持角色隔離：

**Codex Orchestrator ≠ Codex Implementation Agent**

Orchestrator 仍負責：

- 分析
- 規劃
- 拆解
- 委派
- Review
- 驗收

Implementation Agent 僅負責實作。

### Fallback 模型 Routing

由 Codex 根據**已拆解後的 Implementation Task**複雜度選擇模型與 reasoning level。

#### Luna

小型、明確、低風險任務優先使用 Luna。

適用：

- 文案修改
- UI 微調
- CSS
- 欄位新增或調整
- 明確 Function 修改
- 依照既有 Pattern 實作
- 簡單 CRUD
- 簡單 API
- Unit Test
- Rename
- 小型 Refactor
- 明確的小型 Bug Fix
- 已完成驗收後的機械性 Git 交付操作

使用 Luna 的前提：

- Goal 明確
- Scope 明確
- 修改範圍有限
- Acceptance Criteria 明確
- 不需要重新理解整個架構

Luna 的 reasoning level **不固定使用 Max**，由 Codex 根據任務難度選擇；簡單操作應優先使用較低且足夠的 reasoning level。

#### Terra

中等複雜度 Implementation Task 使用 Terra。

適用：

- 多檔案修改
- 一般跨模組功能
- 中型 Refactor
- 有一定程度的業務邏輯
- 需要理解部分架構
- Luna 執行失敗且判斷與模型能力有關
- 較複雜的 merge / rebase conflict

#### Sol

只有複雜、高風險或需要大量推理的 Implementation Task 才使用 Sol。

適用：

- 大型跨模組修改
- 核心架構實作
- Authentication / Security
- 複雜 Concurrency
- 複雜 Async Flow
- 大型 Refactor
- 原因不明且難以定位的 Bug
- Terra 無法可靠完成的任務

應避免因為 Sol 能力最強，就預設所有 Fallback Implementation Task 都使用 Sol。

核心原則：

> **先由 SOL High 將複雜問題拆成簡單問題，再盡可能讓 Luna / Terra 完成 Fallback 實作。**

---

## 8. Implementation Agent 職責

無論使用：

- agy
- Codex Subagent（僅 Fallback）
- Codex CLI Luna（僅 Fallback）
- Codex CLI Terra（僅 Fallback）
- Codex CLI Sol（僅 Fallback）

Implementation Agent 均負責：

- 閱讀 Task 指定的相關程式碼
- 執行實作
- 修改程式碼
- 新增必要程式碼
- Refactor
- 撰寫或修改測試
- Build
- Test
- Lint / Type Check
- 處理實作過程中的一般錯誤

Implementation Agent 不應自行改變：

- 原始需求
- 核心架構方向
- Task Scope
- Acceptance Criteria

若發現 Codex 的規劃與實際 Repository 存在重大衝突，應停止擴大修改並回報 Codex。

---

## 9. Implementation Agent 回傳格式

為降低 Codex Token 消耗，Implementation Agent 完成後應使用精簡、結構化格式回報：

### Result
`SUCCESS` / `FAILED` / `BLOCKED`

### Changed
修改的檔案清單。

### Summary
簡短說明實作內容。

### Validation
執行的 Build / Test / Lint / Type Check 與結果。

### Issues
尚未解決的問題；沒有則填 `None`。

### Notes
只有 Codex 必須知道的額外資訊。

預設不回傳：

- 完整程式碼
- 完整 Diff
- 完整 Build Log
- 完整 Test Log
- 大量 Repository 內容
- 詳細推理過程
- 與驗收無關的分析內容

Codex 若需要更多資訊，再針對特定部分要求補充。

---

## 10. 失敗與升級策略

Implementation Task 失敗時，Codex 不應直接自行修改。

Codex 應先判斷：

### 指令不足
補充 Context / Requirements / Acceptance Criteria 後重新委派。

### Task 過大
進一步拆分後重新委派。

### 實作錯誤
將具體錯誤與驗證結果交回 Implementation Agent 修正。

### 模型能力不足
升級 Implementation Model。

Codex CLI 模型升級順序原則：

**Luna → Terra → Sol**

但不要求每次都逐級升級。

如果 Codex 已判斷任務明顯超出較低模型的合理能力，可以直接選擇適合的模型。

禁止在沒有分析失敗原因的情況下，使用完全相同的 Prompt 與配置反覆重試。

---

## 11. Codex 驗證規範

Implementation Agent 回報 `SUCCESS` 不代表任務已完成。

Codex 必須進行必要的獨立驗證。

優先使用：

- `git status`
- `git diff --stat`
- 精確範圍 `git diff`
- Build / Test
- Lint / Type Check
- Acceptance Criteria 驗證

Codex Review 應集中在：

- 是否符合原始需求
- 是否符合技術規劃
- 是否超出 Task Scope
- 是否存在不必要修改
- 關鍵邏輯是否正確
- 是否破壞既有功能
- 是否存在 Regression
- Acceptance Criteria 是否成立

一般低風險任務不需要重新進行 Implementation Agent 已完成的全部分析。

---

## 12. Token 與輸出衛生規範

### Codex

- 優先使用 `rg` 精確定位。
- 僅讀取必要上下文。
- 避免整庫掃描。
- 避免重複讀取。
- 避免輸出完整大型 Diff。
- Build / Test 預設使用 quiet / concise 模式。
- 優先取得 Exit Code、錯誤摘要與失敗測試。
- 僅在 Debug 必要時提高 verbose level。

### Implementation Agent

- 優先使用 `rg` 搜尋。
- 只讀取 Task 所需內容。
- 避免無差別載入 Repository。
- Build / Test 優先使用 quiet 模式。
- 一般實作錯誤應先自行修正。
- 最終只回傳 Codex 驗收需要的資訊。

---

## 13. 最終完成條件

只有 Codex Orchestrator 可以判定整體需求完成。

回報完成前必須確認：

1. 所有 Implementation Task 已完成。
2. 實作符合原始需求。
3. 實作符合 Codex 技術規劃。
4. 沒有非預期修改。
5. 必要 Build / Test / Lint / Type Check 已通過。
6. 沒有已知 Regression。
7. 所有 Acceptance Criteria 均成立。

若其中任何一項不成立，Codex 必須繼續處理，不得回報完成。

---

## 14. 核心原則

完整責任鏈：

**User**

↓

**Codex SOL High**
- Requirement Analysis
- Architecture
- Planning
- Task Decomposition

↓

**Implementation Routing**

**強制第一優先：agy**
- Gemini 3.6 Flash
- Claude Sonnet 4.6
- 任一 agy 模型可用時，禁止使用 Codex Implementation Agent

**僅當所有 agy 模型均不可用：Codex Implementation Fallback**
- Codex Subagent / Codex CLI
- Luna → 小型 / 明確（reasoning 依任務決定）
- Terra → 中型
- Sol → 複雜 / 高風險

↓

**Implementation Agent**
- Code
- Build
- Test
- Debug

↓

**Codex SOL High**
- Review
- Integration Validation
- Acceptance

↓

**Done**

最重要的資源分配原則：

> **Codex SOL High 的 Token 用於提高決策品質；Implementation Agent 的 Token 用於承擔實作工作量。**

> **先用強模型把複雜問題拆成簡單且明確的問題，再將實作交給成本與能力最匹配的模型。**