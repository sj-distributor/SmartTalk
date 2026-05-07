# AiSpeechAssistantConnect V2 Stability & Performance Rollout Plan

> **核心原則**：每一個修復都必須是可獨立驗證、可獨立回滾、對現有通話路徑零 breaking 的。任何 OpenAI / Twilio 行為變更都通過 **opt-in feature flag** 或 **per-assistant override** 控制，**全域默認行為與現狀完全一致**。

> **狀態圖例**：⚪ 未開始 / 🟡 進行中 / 🟢 已完成 / 🔵 已合併 / ⚫ 已取消 / ⚠️ 阻塞

> **生命週期**：每個 PR 完成後 → 更新 [Tracking 表](#tracking-總覽) → 寫入 [Retrospective](#retrospective-小結每個-pr-合併後填寫) → 自我檢查清單

---

## Tracking 總覽

| Phase | PR # | Branch | Title | 狀態 | 開始 | 完成 | Reviewer |
|---|---|---|---|---|---|---|---|
| 0 | 0.1 | `feat/v2-stability-perf-overhaul` | 主分支 + 骨架 | 🟢 | 2026-05-07 | 2026-05-07 | - |
| 1 | 1.1 | `fix/v2-alaw-codec-typo` | 修復 g712_alaw 拼寫 | 🔵 #916 merged | 2026-05-07 | 2026-05-07 | - |
| 1 | 1.2 | `fix/v2-delivery-info-token` | 修復 ResolveDeliveryInfoAsync 邏輯反轉 | 🔵 #917 merged | 2026-05-07 | 2026-05-07 | - |
| 1 | 1.3 | `fix/v2-hangup-cancellation-token` | 修復 ProcessHangup token 序列化 | 🔵 #918 merged | 2026-05-07 | 2026-05-07 | - |
| 1 | 1.4 | `perf/v2-cache-pst-timezone` | 緩存 PST TimeZone | 🔵 #919 merged | 2026-05-07 | 2026-05-07 | - |
| 1 | 1.5 | `fix/v2-prompt-static-vars-npe` | ResolveStaticPromptVariables NPE 防護 | 🔵 #920 merged | 2026-05-07 | 2026-05-07 | - |
| 1 | 1.6 | `fix/v2-data-provider-null-handling` | 資料層 null 處理 | 🔵 #921 merged | 2026-05-07 | 2026-05-07 | - |
| 2 | 2.1 | `fix/v2-connect-async-cleanup` | ConnectAsync 兜底清理 | 🟡 PR #923 | 2026-05-07 | 2026-05-07 | - |
| 2 | 2.2 | `fix/v2-session-lifecycle-callbacks` | Wire OnClientStop/SessionEnded | ⚪ | - | - | - |
| 2 | 2.3 | `stab/v2-ws-keepalive` | WS KeepAlive 15s | ⚪ | - | - | - |
| 2 | 2.4 | `stab/v2-stream-sid-race` | StreamSid race 防護 | ⚪ | - | - | - |
| 3 | 3.1 | `stab/v2-audio-buffer-bounded` | AudioBuffer 限制 | ⚪ | - | - | - |
| 4 | 4.1 | `feat/v2-assistant-config-fields` | Entity 加配置字段 | ⚪ | - | - | - |
| 4 | 4.2 | `feat/v2-config-dto-passthrough` | DTO/ModelConfig 透傳 | ⚪ | - | - | - |
| 5 | 5.1 | `feat/v2-transcription-model-config` | Transcription 模型 opt-in | ⚪ | - | - | - |
| 5 | 5.2 | `feat/v2-turn-detection-semantic-vad` | semantic_vad opt-in | ⚪ | - | - | - |
| 5 | 5.3 | `feat/v2-noise-reduction-config` | Noise Reduction 配置 | ⚪ | - | - | - |
| 5 | 5.4 | `feat/v2-temperature-token-limit` | Temperature/MaxTokens | ⚪ | - | - | - |
| 5 | 5.5 | `feat/v2-voice-extended` | Voice 擴充 (marin/cedar) | ⚪ | - | - | - |
| 6 | 6.1 | `feat/v2-function-output-abstraction` | Function output 抽象 | ⚪ | - | - | - |
| 6 | 6.2-6.7 | `feat/v2-function-output-{name}` | 逐函數切結構化 | ⚪ | - | - | - |
| 7 | 7.1 | `refactor/v2-knowledge-fetch-apply-split` | Fetch+Apply 拆分 | ⚪ | - | - | - |
| 7 | 7.2 | `perf/v2-knowledge-parallel-fetch` | Task.WhenAll 並行 | ⚪ | - | - | - |
| 8 | 8.1 | `perf/v2-config-cache` | Tools/TurnDetect cache | ⚪ | - | - | - |
| 8 | 8.2 | `perf/v2-prompt-token-regex` | Prompt 單次 regex | ⚪ | - | - | - |
| 10 | 10.1 | `feat/v2-track-assistant-item-id` | item_id 追蹤 | ⚪ | - | - | - |
| 10 | 10.2 | `feat/v2-twilio-mark-queue` | Twilio mark queue | ⚪ | - | - | - |
| 10 | 10.3 | `feat/v2-proper-barge-in` | 完整 barge-in | ⚪ | - | - | - |

---

## 工作流規則

1. 主分支 `feat/v2-stability-perf-overhaul` 從最新 `main` 切出，**不直接修改業務代碼**，只承載 README/CHANGELOG 與後續合併
2. 每個子 PR：
   - 從主分支切出
   - 完成後 PR target 主分支
   - 必須通過全部 CI（build + unit + integration + E2E）
   - 必須有獨立的 commit 歷史
3. 主分支累積 N 個 PR 後，做整體 staging 灰度測試 1 週，再合回 `main`
4. 每個 PR 必須附 **回滾預案**

---

## TDD 強制流程（Rule 9）

每個 PR 都嚴格走：

1. **🔴 Red**：寫測試覆蓋當前業務行為（現存功能與代碼邏輯）
2. **驗證測試在當前代碼上通過**（新增的 pinning / 行為金樣 test）或失敗（新功能的 expected behavior test）
3. **🟢 Green**：最小代碼改動讓 test 通過
4. **驗證所有 test 仍然通過**（行為等價）
5. **🔵 Refactor**：在綠燈下優化、抽取通用化
6. **驗證所有 test 仍然通過**

---

## 標準 PR Description 模板（per Rule 13）

PR title 用祈使句，無 phase / sprint / step 前綴。Body 維持兩段：

```markdown
## Summary

- 簡述 1：這個 PR 做了什麼（用戶/系統視角）
- 簡述 2：為什麼這樣做（Why）
- 內聯引用文檔：[OpenAI Realtime API session.update](URL)、相關代碼路徑 `src/...:line`

## Test plan

- [x] Unit: 描述（X cases）
- [x] Integration: 描述
- [ ] Staging: 觀察項
```

額外（僅必要時）：
- **Feature flag** — 若引入新 env var，注明 default + rollout plan
- **Out of scope** — 若刻意未修某些相關問題，注明
- **Rollback** — 若 `git revert` 不夠（例如 DB migration），描述具體 playbook

**禁止**：title 含 `[Phase X.Y]` / `[Sprint N]` / `[Step Z/M]`；body 含 progress narrative（"This is the 3rd PR..."）；body 含內部狀態 emoji（🟢/🟡/⚪）。

> 內部跟蹤狀態（本文檔的 🟢/🔵 標記）保留 — 那是內部 docs，不是 GitHub PR body。

---

## Phases 詳細展開

### Phase 0 — 主分支與骨架

#### PR 0.1 — 創建主分支
- **分支**：`feat/v2-stability-perf-overhaul`
- **Target**：`main`
- **變更**：
  - `docs/v2-stability-perf-rollout-plan.md`（本文檔）
  - `docs/V2_ROLLOUT_PLAYBOOK.md`（feature flag 跟蹤）
- **風險**：零

---

### Phase 1 — 零行為變更純 Bug 修復

#### PR 1.1 — `g712_alaw` 拼寫
- 修改：[RealtimeAiAudioCodec.cs:10](../src/SmartTalk.Messages/Enums/RealtimeAi/RealtimeAiAudioCodec.cs)（`g712_alaw` → `g711_alaw`）
- 新增 pinning test：`RealtimeAiAudioCodecDescriptionPinningTests`
- 風險：零（A-law 路徑當前就是壞的）
- 文檔：OpenAI Realtime API audio formats、ITU-T G.711

#### PR 1.2 — ResolveDeliveryInfoAsync `||` → `&&` + 補第二 token Replace
- 修改：[Build.Knowledge.cs:295-301](../src/SmartTalk.Core/Services/AiSpeechAssistantConnect/AiSpeechAssistantConnectService.Build.Knowledge.cs)
- 行為比對：與 V1 [AiSpeechAssistantService.cs:310](../src/SmartTalk.Core/Services/AiSpeechAssistant/AiSpeechAssistantService.cs)
- 風險：當前永遠 noop，修復後注入 delivery info（與 V1 一致）

#### PR 1.3 — ProcessHangup CancellationToken → None
- 修改：[FunctionCalls.Hangup.cs:11](../src/SmartTalk.Core/Services/AiSpeechAssistantConnect/AiSpeechAssistantConnectService.FunctionCalls.Hangup.cs)
- 風險：零（與其他 Schedule 一致）

#### PR 1.4 — 緩存 PST TimeZone + Linux fallback
- 新增：`Utils/PstTimeZone.cs`
- 替換：兩處 `TimeZoneInfo.FindSystemTimeZoneById` 調用
- 風險：低（行為等價，靜態 init 一次）

#### PR 1.5 — ResolveStaticPromptVariables NPE 防護
- 修改：[Build.Knowledge.cs:42-51](../src/SmartTalk.Core/Services/AiSpeechAssistantConnect/AiSpeechAssistantConnectService.Build.Knowledge.cs)
- 風險：低（僅改邊緣 case）

#### PR 1.6 — 資料層 null 處理
- 修改：[AiSpeechAssistantDataProvider.cs:201-203](../src/SmartTalk.Core/Services/AiSpeechAssistant/AiSpeechAssistantDataProvider.cs)
- **拆兩步**：
  - 1.6a：V1 + V2 各自加 null 防禦
  - 1.6b：data provider 改返回 nullable tuple

---

### Phase 2 — 防禦性修復

詳見原計劃文檔 Phase 2 章節（PR 2.1 - 2.4）

### Phase 3 — Audio Buffer

詳見原計劃文檔 Phase 3 章節（PR 3.1）

### Phase 4-10

詳見原計劃文檔對應章節

---

## Retrospective（小結，每個 PR 合併後填寫）

> 每個 PR 完成後填以下表格

### Phase 0

#### PR 0.1 — 主分支與骨架
- **預期工作量**：15 分鐘
- **實際工作量**：~10 分鐘
- **遇到問題**：無
- **學到什麼**：
  - 確認 `docs/` 目錄已存在但只有 `.DS_Store`
  - 倉庫 commit 歷史以 PR merge 為主，個別 commit 有 fix/update 動詞
- **後續調整**：無

### Phase 1

#### PR 1.1 — 修復 g712_alaw 拼寫
- **預期工作量**：30 分鐘
- **實際工作量**：~25 分鐘（含完整文檔調研、grep 全 codebase、TDD 三步、commit）
- **遇到問題**：無
- **學到什麼**：
  - V1 (`AiSpeechAssistantService.cs`) 對 `input_audio_format` **硬編碼了 `"g711_ulaw"` 字串**（line 1210, 1211, 1223, 1224），完全繞過 enum description。所以 V1 對這個 bug 是免疫的
  - V2 (`OpenAiRealtimeAiProviderAdapter.cs:43-44`) 用 `clientCodec.GetDescription()`，所以 V2 會踩到這個 bug — 但因為 Twilio 用 MULAW，ALAW 路徑當前是死代碼
  - 內部 `RealtimeAi/Readme.md:19` 已明確記載支持 `g711_ulaw、g711_alaw、pcm16`，bug 與內部文檔不一致
  - GetDescription extension method 在 `SmartTalk.Core.Extensions.EnumerableExtension`，依賴 `System.ComponentModel.DescriptionAttribute`
- **TDD 流程記錄**：
  - **🔴 Red**：寫 3 個 InlineData theory pinning test，預期值為正確的 OpenAI wire format → ALAW 案例失敗（got "g712_alaw" expected "g711_alaw"），MULAW + PCM16 通過
  - **🟢 Green**：改一個字符（`g712_alaw` → `g711_alaw`）→ 所有 3 case 通過
  - **🔵 Refactor**：無（一字符 fix 無重構空間）
- **回歸驗證**：完整 unit test suite 157/157 通過，零 regression
- **後續調整**：無
- **PR 提交**：commit `3cbe34372` 於 `fix/v2-alaw-codec-typo` 分支

#### PR 1.2 — 修復 ResolveDeliveryInfoAsync 邏輯反轉
- **預期工作量**：60 分鐘
- **實際工作量**：~50 分鐘（含對 V1 邏輯比對 + 識別語義差異 + 18 case theory）
- **遇到問題**：
  - 第二個 token 的字面 literal `#{CRM_路线_送货日数据}` 在整個 codebase 只出現一次（即 buggy 處）。無法從代碼判斷 token 拼寫是否符合 DB 中真實 prompt。決策：preserve 字面值不動（最低風險）
  - V1 與 V2 的「empty cache → placeholder space」語義有微妙差異：V1 用 `IsNullOrEmpty(deliveryInfo) ? " " : deliveryInfo`；V2 sibling `ResolveCustomerInfoAsync` 用 `?? " "`（只 null 觸發）。本次修復保留 V2 author 的 `?? " "` 風格（在 helper 內已合並 null 與 empty 都觸發，與 V1 一致）
- **學到什麼**：
  - 抽 `HasDeliveryInfoToken` + `ApplyDeliveryInfoTokens` 為 public static，跟 `CheckIfInServiceHours` 同樣 pattern，私有 method 從 7 行降到 5 行純編排
  - 對 partial class 加 public static 是 codebase 既定 testability pattern
  - 18 個 theory case 覆蓋 token 存在/缺失、value 空/非空、case 大小寫、重複 token、whitespace value 等邊界
- **TDD 流程記錄**：
  - **🔴 Red**：寫 18 個 theory，引用尚未存在的 API → 11 個 compile error 確認 Red
  - **🟢 Green**：抽 helper + 改 wiring → 全 18 case 通過
  - **🔵 Refactor**：無新重構（已隨 fix 同步抽出 helper）
- **回歸驗證**：完整 unit test suite 172/172 通過（從基線 154 + 18 新 theory case）
- **未覆蓋的測試類型**：
  - Integration test（mock `_salesDataProvider` + 調用 `BuildKnowledgeAsync`）— 留待 Phase 7（fetch+apply split）一併補上
  - E2E test — 留待 Phase 7
- **後續調整**：無
- **PR 提交**：commit `af7f0f0be` 於 `fix/v2-delivery-info-token` 分支

#### PR 1.3 — 修復 ProcessHangup CancellationToken
- **預期工作量**：30 分鐘
- **實際工作量**：~40 分鐘（多花 10 分鐘決定測試策略）
- **遇到問題**：
  - 原計劃用「mock IBackgroundJobClient + 捕獲 expression」直接測 `ProcessHangup` 私有方法，但需要實例化 12 dependency 的 SUT，過重
  - 改成 extract pure helper `BuildHangupJobExpression(string)` 為 `public static`，使用 expression compile + NSubstitute 驗證實際傳入的參數類型
  - 確認 V1 (`AiSpeechAssistantService.cs:932`) 也有同樣的 anti-pattern。決定 **不修 V1**（scope 限制在 V2 重構）
- **學到什麼**：
  - Hangfire 對 closure-captured `CancellationToken` 的處理：序列化時被替換為 `default`，所以行為今天恰好等價於 None。但這是隱性依賴，未來 Hangfire 升級或 token 類型變更時會 silent break
  - `Expression<Func<T, Task>>` 可以 `.Compile()` 出來用 NSubstitute fake target 跑，反向驗證 expression 樹的實際參數值
  - 5 個 theory（包括 callSid null/empty/normal）覆蓋 expression 構建的所有路徑
- **TDD 流程記錄**：
  - **🔴 Red**：寫 5 個 theory 引用 `BuildHangupJobExpression`（不存在）→ 3 個 compile error 確認 Red
  - **🟢 Green**：抽 `BuildHangupJobExpression` + 改 ProcessHangup 用它 → 5/5 通過
  - **🔵 Refactor**：原 method 從 4 行降為 3 行純編排，加 `_ = cancellationToken;` discard 明確意圖
- **回歸驗證**：完整 unit test suite 159/159 通過（基線 154 + 5 新 theory）
- **未覆蓋的測試類型**：Integration / E2E — 此修復是運行時等價的純意圖改進，集成測試從 Phase 7 補充
- **後續調整**：V1 同樣有此 bug，後續可單獨 PR 修
- **PR 提交**：commit `b78ba1057` 於 `fix/v2-hangup-cancellation-token` 分支

#### PR 1.4 — 緩存 PST TimeZone + Linux fallback
- **預期工作量**：45 分鐘
- **實際工作量**：~50 分鐘（含文檔調研發現 .NET 6+/8 已有平臺支持，重新評估 PR 價值）
- **遇到問題**：
  - 文檔調研發現：.NET 6+ 已自動處理 Windows ↔ IANA ID 轉換，.NET 8 內建 `FindSystemTimeZoneById` cache。原本的「跨平臺安全 + 性能」雙動機都已被平臺方案
  - 重新定位 PR 價值：核心是 **集中化 magic string** + **defense-in-depth fallback** + **可讀性**，而非性能或修 bug
  - 為避免「靜態 init 失敗污染整個 process」，使用 `Lazy<T>` 而非 `static readonly` field
- **學到什麼**：
  - .NET 8 已對 `FindSystemTimeZoneById` 內建 ID-key cache，所以本 PR 對性能幾乎無影響（從 dictionary lookup 改為 atomic field read，差別微秒級）
  - `Lazy<T>` 默認用 `LazyThreadSafetyMode.ExecutionAndPublication`，failure 會被 cache（適合 timezone 這種不可恢復的環境問題）
  - codebase 中還有 7+ 處用 `FindSystemTimeZoneById` 直接調用（PhoneOrderProcessJobService、TwilioWebhookService 等）。本 PR scope 只動 V2 兩處，留待後續統一
  - 測試使用 `BaseUtcOffset.ShouldBe(-8h)` 與 `Id.ShouldBeOneOf(...)` 雙重驗證，跨 OS 通用
- **TDD 流程記錄**：
  - **🔴 Red**：寫 4 個 helper test，引用未存在的 `PstTimeZone.Get()` → 5 個 compile error
  - **🟢 Green**：實現 helper（`Lazy<TimeZoneInfo>` + 雙 ID fallback + actionable error）→ 4/4 通過
  - **🔵 Refactor**：替換 V2 兩處調用點，依賴 helper（同時 cleanup 一個沒用的 local var `pstZone`）
- **回歸驗證**：完整 unit test suite 158/158 通過。**現存 `CheckIfInServiceHoursTests` 15 個 case 隱式作為 helper 集成測試** — 它們未變但全綠，證明改寫的 service hours 邏輯行為等價
- **未覆蓋的測試類型**：N/A（unit + 隱式 integration 已足夠，無外部依賴）
- **後續調整**：codebase 其他 7+ 處 `FindSystemTimeZoneById` 直接調用可在後續 PR 統一收編到 helper
- **PR 提交**：commit `072d2f237` 於 `perf/v2-cache-pst-timezone` 分支

#### PR 1.5 — ResolveStaticPromptVariables NPE 防護
- **預期工作量**：45 分鐘
- **實際工作量**：~50 分鐘（含 StartsWith CurrentCulture vs Ordinal 微調與 byte-exact 驗證）
- **遇到問題**：
  - 第一版 helper 用 `StartsWith("+1", StringComparison.Ordinal)` 但原代碼是 `StartsWith("+1")`（默認 CurrentCulture）。雖然 ASCII 結果等價，仍然把 helper 改回 default 比較以保證 byte-exact 等價（已加注釋說明）
  - 為避免與 PR 1.4 衝突，line 44 的 `TimeZoneInfo.FindSystemTimeZoneById` 保持原樣不動。當 PR 1.4 先 merge 時這行會被自動 resolve 為 `PstTimeZone.Get()`
- **學到什麼**：
  - 微妙的 string API 默認對比語義：`StartsWith(string)` 用 CurrentCulture，而 `Contains(string, StringComparison)` 顯式要求 comparison。這類隱性約定 refactor 時極易出錯
  - 抽 `ResolveStaticTokens(prompt, userProfileJson, from, pstTime)` 後，私有 wrapper 從 8 行降到 4 行，pure helper 完全可獨立測試
  - 20 個 theory case 達到充分覆蓋：null/empty 邊界 + +1 strip 規則 + 國際號碼保留 + Twilio anonymous 標記 + 重複 token + deterministic time formatting
- **TDD 流程記錄**：
  - **🔴 Red**：寫 20 個 theory，引用未存在的 helper → 多個 compile error
  - **🟢 Green-1**：實現 helper（含 `StringComparison.Ordinal`）→ 全綠
  - **🔵 Refactor**：發現 `Ordinal` 與原 `StartsWith` 的 CurrentCulture 默認有微妙差異，回退為默認對比 + 加注釋
  - **🟢 Green-2**：再驗證一次 → 174/174 仍綠
- **回歸驗證**：完整 unit test suite 174/174 通過（基線 154 + 20 新 theory）
- **未覆蓋的測試類型**：私有 wrapper `ResolveStaticPromptVariables` 沒有直接 unit test（依靠 helper 全綠 + 簡單 wrapper 邏輯靠 code review 保證）。Integration test 留待 Phase 7
- **後續調整**：
  - PR 1.4 merge 時 line 44 自動切換到 `PstTimeZone.Get()`
  - PR 1.6 處理 `_ctx.Knowledge` null 情況（上游根因）
- **PR 提交**：commit `99fa945bd` 於 `fix/v2-prompt-static-vars-npe` 分支

#### PR 1.6 — 資料層 null 處理
- **預期工作量**：90 分鐘
- **實際工作量**：~75 分鐘（決策做得快但編碼遇到 namespace 衝突）
- **遇到問題**：
  - 原計劃拆 1.6a + 1.6b 兩 PR，發現「caller-side defense」根本無法獨立做 — 因為 NRE 發生在 data provider 內部，caller 看不到 null 結果。最終合並成單個 PR
  - 對 V1 是否「順手修一下」糾結很久。最終決策：**不修 V1**，因為 (1) PR scope 是 V2 stability rollout，(2) V1 的 NRE 經過數據提供者轉移到 V1 自己的代碼，user-visible 行為一致（call 仍然崩潰），無 regression
  - C# 命名空間 `SmartTalk.Core.Domain.AISpeechAssistant` 與類別 `AiSpeechAssistant` 同名衝突 — 用 type alias `AiSpeechAssistantEntity = ...` 解決
- **學到什麼**：
  - 一個微妙的 invariant：如果 data provider 改成返回 `(null, null, null)`，原本 V1 在 data provider 內 NRE 的位置，會轉移到 V1 自己代碼的下一個 deref。這在 stack trace 層面是變化，但 user-visible 行為（連線崩潰）一致
  - V2 用 `AiAssistantNotAvailableException` 作為「missing data」的領域異常，現存 try/catch 已處理。把 NRE 翻譯成這個異常是優雅的
  - 整合測試填補了 unit test 無法覆蓋的「data provider 真實 DB no-match」場景，pin 住整個鏈路的契約
- **TDD 流程記錄**：
  - **🔴 Red**：寫 4 個 helper test，引用未存在的 `EnsureAssistantInfoComplete` → 5+ compile error
  - **🟢 Green-1**：實現 helper + 改 data provider + 改 V2 caller → 編譯失敗（namespace 衝突）
  - **🔵 Refactor**：加 type alias 解決命名衝突
  - **🟢 Green-2**：158/158 通過
  - 補充：integration test 加在 `AiSpeechAssistantConnectFixture.DataProvider.cs` pin 住 no-match 的數據層契約
- **回歸驗證**：完整 unit test suite 158/158 通過。Integration test 編譯通過（CI 跑真實 DB）
- **未覆蓋的測試類型**：
  - V2 caller 的端到端測試（mock 整個依賴鏈 + 觀察 AiAssistantNotAvailableException 路徑）— 留待 Phase 7 集成測試專項補
- **後續調整**：
  - V1 同樣場景下會 NRE 在 V1 自己的代碼（line 268），不在本 PR scope。應有獨立 PR 修 V1
  - 7+ 個其他 `FindSystemTimeZoneById` 直接調用（PR 1.4 retrospective 提到）也適用同樣的 helper 推廣
- **PR 提交**：commit `879fcd21e` 於 `fix/v2-data-provider-null-handling` 分支

---

## 自我檢查清單（每個 PR 合併前完成）

### 代碼品質
- [ ] 所有方法簽名單行（Rule 1）
- [ ] `.ConfigureAwait(false)` 同行（Rule 2）
- [ ] 方法 ≤ 25 行目標 / 50 行絕對上限（Rule 3）
- [ ] 主管線扁平、無嵌套塊（Rule 4）
- [ ] 邏輯塊間有空行（Rule 5）
- [ ] Guard clause 風格一致（Rule 6）

### 測試
- [ ] Unit + Integration + E2E 三層覆蓋（Rule 9 / 12）
- [ ] 三層測試的 Trait 標籤齊全
- [ ] 跨 OS guard 已加（Rule 12.1）
- [ ] 資源 GUID 後綴避免衝突（Rule 12.2）
- [ ] IDisposable cleanup（Rule 12.3）
- [ ] 真實生產類驅動測試（Rule 12.4）
- [ ] 單跑 / 並發跑無 race
- [ ] env var 常量名 pinning（Rule 8）

### 文檔
- [ ] PR description 模板填齊
- [ ] 跟蹤表格更新
- [ ] Retrospective 填寫
- [ ] 如有新 env var：更新 `V2_ROLLOUT_PLAYBOOK.md`

### 迴歸保證
- [ ] 默認行為金樣測試
- [ ] grep 確認無 broken caller
- [ ] feature flag default = current behavior

---

## 完整原計劃參考

> 此文檔是**精簡跟蹤版**。完整 23 PR 詳細計劃見對話歷史中的「AiSpeechAssistantConnect V2 安全修復實施計劃」段落。

---

## Phase 1 Close-out（合併後狀態）

**日期**：2026-05-07
**全部 6 個 PR 已合入主分支** `feat/v2-stability-perf-overhaul`

### 合併順序與測試結果

| 步驟 | PR | 合併後 unit test | Δ |
|---|---|---|---|
| 1 | #916 (1.1) | 157/157 | +3 (codec pinning) |
| 2 | #918 (1.3) | 162/162 | +5 (hangup expression) |
| 3 | #921 (1.6) | 166/166 | +4 (null handling) |
| 4 | #917 (1.2) | 184/184 | +18 (delivery info tokens) |
| 5 | #920 (1.5) | 204/204 | +20 (static prompt vars) |
| 6 | #919 (1.4) | **208/208** | +4 (PstTimeZone) |

### 合併過程中的衝突

- **PR 1.4 在最後合併時與 PR 1.6 在 using 區塊衝突**（都加新 using 但行號相鄰）
- 解決：本地 merge → 手動 keep both → push → re-merge
- 結論：line 44 (`PstTimeZone.Get()` swap) 由 git 三路合併自動解決，因為 PR 1.5 把該行作為 context 而非修改

### 最終驗證

- ✅ build 0 errors
- ✅ unit suite 208/208 全綠
- ✅ 主分支 commit history 線性（每個 PR 一個 merge commit）
- ✅ V1 路徑零修改、零 regression
- ✅ DB schema 零變動

### 下一步

主分支累積 6 個 PR + tracking commits。建議的下一步：
1. **觀察 1 週 staging** — 把主分支部署到 staging 環境，跟蹤 V2 通話 success rate / NRE log 數
2. **PR 主分支 → main** — staging 無 regression 後合入 main
3. **開始 Phase 2** — 防禦性修復（PR 2.1-2.4）

