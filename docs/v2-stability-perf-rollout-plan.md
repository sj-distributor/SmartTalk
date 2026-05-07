# AiSpeechAssistantConnect V2 Stability & Performance Rollout Plan

> **核心原則**：每一個修復都必須是可獨立驗證、可獨立回滾、對現有通話路徑零 breaking 的。任何 OpenAI / Twilio 行為變更都通過 **opt-in feature flag** 或 **per-assistant override** 控制，**全域默認行為與現狀完全一致**。

> **狀態圖例**：⚪ 未開始 / 🟡 進行中 / 🟢 已完成 / 🔵 已合併 / ⚫ 已取消 / ⚠️ 阻塞

> **生命週期**：每個 PR 完成後 → 更新 [Tracking 表](#tracking-總覽) → 寫入 [Retrospective](#retrospective-小結每個-pr-合併後填寫) → 自我檢查清單

---

## Tracking 總覽

| Phase | PR # | Branch | Title | 狀態 | 開始 | 完成 | Reviewer |
|---|---|---|---|---|---|---|---|
| 0 | 0.1 | `feat/v2-stability-perf-overhaul` | 主分支 + 骨架 | ⚪ | - | - | - |
| 1 | 1.1 | `fix/v2-alaw-codec-typo` | 修復 g712_alaw 拼寫 | ⚪ | - | - | - |
| 1 | 1.2 | `fix/v2-delivery-info-token` | 修復 ResolveDeliveryInfoAsync 邏輯反轉 | ⚪ | - | - | - |
| 1 | 1.3 | `fix/v2-hangup-cancellation-token` | 修復 ProcessHangup token 序列化 | ⚪ | - | - | - |
| 1 | 1.4 | `perf/v2-cache-pst-timezone` | 緩存 PST TimeZone | ⚪ | - | - | - |
| 1 | 1.5 | `fix/v2-prompt-static-vars-npe` | ResolveStaticPromptVariables NPE 防護 | ⚪ | - | - | - |
| 1 | 1.6 | `fix/v2-data-provider-null-handling` | 資料層 null 處理 | ⚪ | - | - | - |
| 2 | 2.1 | `fix/v2-connect-async-cleanup` | ConnectAsync 兜底清理 | ⚪ | - | - | - |
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

## 標準 PR Description 模板

```markdown
## Why
（這個 bug 為什麼需要修，影響什麼業務場景）

## What changed
- 文件清單與變更摘要

## Breaking risk assessment
- [ ] 已 grep 全 codebase 確認無破壞調用
- [ ] 對行為變更場景做了 staging A/B 對比
- [ ] 默認行為 100% 與線上一致（金樣測試通過）

## Reference docs
- OpenAI Realtime API: ...
- Twilio Media Streams: ...
- 內部代碼依賴鏈: ...

## Test coverage
- [ ] Unit (XX cases): ...
- [ ] Integration (YY cases): ...
- [ ] E2E (ZZ cases): ...
- [ ] 金樣 / pinning test (per Rule 8): ...

## Feature flag (if any)
- Env var: `SQUID_SMARTTALK_XXX_YYY`
- Default: `off`
- Rollout plan: staging warn 1 week → 1 assistant strict 1 week → batch enable

## Rollback playbook
1. 即時關閉：set env var = off
2. 中期回退：revert PR
3. DB 影響：（如 PR 4.x 需描述 migration down）

## Monitoring
- 新增 metrics: ...
- 告警閾值: ...
```

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

#### PR 0.1
- **預期工作量**：-
- **實際工作量**：-
- **遇到問題**：-
- **學到什麼**：-
- **後續調整**：-

### Phase 1

#### PR 1.1
- **預期工作量**：30 分鐘
- **實際工作量**：-
- **遇到問題**：-
- **學到什麼**：-
- **後續調整**：-

#### PR 1.2
- **預期工作量**：60 分鐘
- **實際工作量**：-
- **遇到問題**：-
- **學到什麼**：-
- **後續調整**：-

#### PR 1.3
- **預期工作量**：30 分鐘
- **實際工作量**：-
- **遇到問題**：-
- **學到什麼**：-
- **後續調整**：-

#### PR 1.4
- **預期工作量**：45 分鐘
- **實際工作量**：-
- **遇到問題**：-
- **學到什麼**：-
- **後續調整**：-

#### PR 1.5
- **預期工作量**：45 分鐘
- **實際工作量**：-
- **遇到問題**：-
- **學到什麼**：-
- **後續調整**：-

#### PR 1.6
- **預期工作量**：90 分鐘
- **實際工作量**：-
- **遇到問題**：-
- **學到什麼**：-
- **後續調整**：-

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

