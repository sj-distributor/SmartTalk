# AiSpeechAssistantConnect V2 — Round 2 詳細實施計劃

> **背景**：Phase 1-3（12 PR）已合入 main，hotfix #934（Beta → GA migration）已合入。本輪在 V2-only-in-prod + GA contract 的新基線上，**極度謹慎**地推進穩定性與品質改進。

> **絕對紅線（每一個 PR 都必須遵守）**：
> 1. **默認行為與當前線上 byte-equivalent**：所有新代碼路徑在 DB 配置欄位為 NULL 時，產生與今天**完全相同**的 JSON 與調用順序。每個 PR 必須附「golden payload」迴歸測試證明。
> 2. **不引入 env-var 雙重門**：activation 完全由 DB 欄位驅動 — NULL → 走默認路徑，非 NULL → 該欄位 per-assistant 生效。不為 opt-in feature 引入 env var，避免維護「兩個 source of truth」。例外：純運行時行為切換（如平行 fetch、Beta event sunset、model URL 默認）才考慮 env var。
> 3. **per-field 獨立 activation**：每個 override 欄位獨立生效，互不影響。一個 misconfiguration 不會 cascade 到其他 setting。
> 4. **獨立可逆**：每個 PR 單獨 `git revert` 不留資料遷移殘留（DB 欄位都 NULLABLE，drop 安全）。緊急回滾 = `UPDATE ... SET <field> = NULL WHERE id = <id>` — per-assistant 粒度，不需重啟服務。
> 5. **灰度路徑**：dev → 1 staging assistant → 1 prod canary（低流量）→ 多 prod → 全 prod，每階段觀察 ≥ 48h。

---

## Tracking 總覽

| Phase | PR # | Branch | Title | 默認行為 | 風險 | 工時 | 狀態 |
|---|---|---|---|---|---|---|---|
| P0 | P0.1 | — | hotfix #934 prod 48h 觀察 | — | 零 | 觀察 | 🟡 |
| P0 | P0.2 | — | 處理 PR #929 | — | 零 | 30m | ⚪ |
| 4 | 4.1 | `feat/v2-assistant-config-fields` | Entity 加 NULLABLE 配置欄位 | **完全不變** | 低 | 90m | ⚪ |
| 4 | 4.2 | `feat/v2-config-dto-passthrough` | DTO 透傳（null 時走默認分支） | **完全不變** | 低 | 60m | ⚪ |
| 5 | 5.1 | `feat/v2-transcription-language-hint` | Whisper 語言 hint（混合語） | null → 不變 | 中 | 90m | ⚪ |
| 5 | 5.2 | `feat/v2-semantic-vad-opt-in` | semantic_vad opt-in | null → server_vad | 中 | 75m | ⚪ |
| 5 | 5.3 | `feat/v2-noise-reduction-config` | Noise reduction 配置 | null → 不變 | 低 | 60m | ⚪ |
| 5 | 5.4 | `feat/v2-transcription-model-config` | gpt-4o-transcribe opt-in（成本高） | null → whisper-1 | 低 | 90m | ⚪ |
| 5 | 5.5 | `feat/v2-max-response-tokens` | `max_response_output_tokens` opt-in | null → unlimited | 低 | 45m | ⚪ |
| 5 | 5.6 | `feat/v2-audio-output-speed` | `audio.output.speed` opt-in | null → 1.0 | 低 | 45m | ⚪ |
| 5 | 5.7 | `feat/v2-voice-extended-validation` | Voice 列表擴充 + 驗證 | 列表內 → 不變 | 低 | 60m | ⚪ |
| 6 | 6.1 | `refactor/v2-function-output-base-type` | Function output 抽象基類（不替換） | 完全不變 | 低 | 90m | ⚪ |
| 6 | 6.2-6.7 | `refactor/v2-function-output-{name}` | 逐 function 切類型（golden payload 守護） | 完全不變（byte-equiv） | 中 | 60m × 6 | ⚪ |
| 7 | 7.1 | `refactor/v2-knowledge-fetch-apply-split` | BuildKnowledge fetch/apply 拆分（順序不變） | 完全不變 | 低 | 75m | ⚪ |
| 7 | 7.2 | `perf/v2-knowledge-parallel-fetch` | 並行 fetch（純結構替換） | 行為與順序等價（純讀無 race） | 中 | 90m | ⚪ |
| 8 | 8.1 | `perf/v2-prompt-token-single-pass-regex` | 單次 regex（byte-equiv golden test） | 完全不變 | 低 | 60m | ⚪ |
| 8 | 8.2 | `perf/v2-config-deserialize-cache` | Tools/TurnDetection cache（純記憶化） | cache hit/miss 結構與直接 deserialize 一致 | 低 | 90m | ⚪ |
| 9 | 9.1 | `cleanup/v2-sunset-beta-event-names` | 移除 Beta event-name 雙 case | 2 週觀察後 | 低 | 30m | ⚪ blocked |
| 9 | 9.2 | `feat/v2-tracing-debug-opt-in` | `session.tracing=auto` opt-in | null → 不發 | 零 | 60m | ⚪ |
| 9 | 9.3 | `feat/v2-response-usage-tracking` | `response.done.usage` 抽取 + 持久化 | 純讀 + 寫新欄位 | 低 | 90m | ⚪ |
| 10 | 10.1 | `feat/v2-item-id-tracking-additive` | item_id 追蹤（純增量，不改邏輯） | 完全不變 | 低 | 75m | ⚪ |
| 10 | 10.2 | `feat/v2-twilio-mark-queue-additive` | Twilio mark queue（純發 mark，不用） | 完全不變 | 低 | 90m | ⚪ |
| 10 | 10.3 | `feat/v2-barge-in-precise-truncate` | 用 10.1+10.2 做精確 truncate（per-assistant bool） | `enable_precise_barge_in` null/false → legacy | **高** | 90m | ⚪ |
| 11 | 11.1 | `feat/v2-default-url-realtime-2-canary` | gpt-realtime-2 per-assistant override | 不動 DefaultUrl | 低 | 30m | ⚪ |
| 11 | 11.2 | `feat/v2-default-url-realtime-2-flip` | 1 週 canary 後 flip DefaultUrl | 線上行為改變 | 中 | 30m | ⚪ blocked |

---

## Phase 0 — Pre-flight（必須先做）

### P0.1 — hotfix #934 prod 48h 觀察
- **不做代碼改動**
- **觀察項**（必須全綠才能進入 Phase 4）：
  - 任何 OpenAI WS 連接的 `invalid_beta` 錯誤 → **必須為 0**
  - 通話完成率（Twilio CallStatus.completed / total）→ **≥ hotfix 前基線**
  - 平均通話時長 → **無顯著下降**
  - 用戶投訴 → **無 GA-related 新投訴**
- **如有任何項異常**：立即 rollback hotfix（PR #934 是純 V1+V2 對 OpenAI 的修改，revert 後恢復 hotfix 前狀態。但 hotfix 前狀態本身是壞的 — 所以實際是「等 OpenAI 端問題」而不是 revert hotfix）

### P0.2 — PR #929 處理
- **推薦方案 (c)**：直接 close PR #929，標註「superseded by Phase 11 of round 2」。理由：
  - PR #929 是在 hotfix 之前做的，base 已過期
  - gpt-realtime-2 對 transcription/voice/speed 配置敏感；Phase 5 配置可調後一次 A/B
  - Phase 11.1 重做時 codebase 已是新基線，避免 rebase 衝突
- **行動**：`gh pr close 929 --comment "Superseded by Phase 11..."`

---

## Phase 4 — 配置面板鋪設（不可見 / 零行為變更）

> **整個 Phase 4 不改變任何用戶可見行為**。只是為 Phase 5 鋪 plumbing。Phase 4 兩個 PR 合入後，所有 assistant 的線上行為與今天 byte-equivalent。

### PR 4.1 — Entity 加 NULLABLE 配置欄位

**動機**：所有 Phase 5 opt-in 需要 per-assistant 配置存儲。本 PR 只加欄位，不讀也不寫。

**修改**：
- `AiSpeechAssistant.cs` 加以下 nullable 欄位：
  ```csharp
  [Column("transcription_model"), StringLength(64)]
  public string TranscriptionModel { get; set; }              // null → whisper-1 默認

  [Column("transcription_language"), StringLength(8)]
  public string TranscriptionLanguage { get; set; }            // null → 不發 hint

  [Column("turn_detection_type"), StringLength(32)]
  public string TurnDetectionType { get; set; }                // null → 走當前 DeserializeFunctionCallConfig 邏輯

  [Column("turn_detection_threshold")]
  public decimal? TurnDetectionThreshold { get; set; }

  [Column("turn_detection_silence_ms")]
  public int? TurnDetectionSilenceMs { get; set; }

  [Column("input_noise_reduction_type"), StringLength(32)]
  public string InputNoiseReductionType { get; set; }          // null → 走當前邏輯

  [Column("max_response_output_tokens")]
  public int? MaxResponseOutputTokens { get; set; }            // null → 不發

  [Column("output_audio_speed")]
  public decimal? OutputAudioSpeed { get; set; }               // null → 不發
  ```
- **DB Migration**：純 ADD COLUMN，全部 NULLABLE，零 backfill

**默認行為保證**：
- 本 PR **不修改** `BuildSessionOptions` 或任何 adapter — 新欄位讀都不讀
- 所有新欄位現有 row 為 null，相當於不存在

**TDD**：
1. Entity property pinning test（防止重命名）— 8 cases
2. Integration test：插入 row 帶所有 null + 帶所有非 null 兩種，讀回比對
3. Migration test：up-then-down 確認 schema 可逆

**回滾**：`drop column` × 8（NULLABLE 故無資料丟失風險）

**驗證信號**：所有現有 unit + integration 測試通過（無新行為，必然通過）

---

### PR 4.2 — DTO + ModelConfig 透傳（默認分支顯式守護）

**動機**：把 4.1 的 entity 欄位流到 `RealtimeAiModelConfig`，並在 adapter 中**顯式守護**「null 時走原邏輯」。

**修改**：
1. `AiSpeechAssistantDto` 加同名 nullable 欄位
2. `RealtimeAiModelConfig`（V2 namespace）加同名 nullable 欄位
3. `BuildSessionOptions` 把 entity → ModelConfig 一條條複製：
   ```csharp
   TranscriptionModel = assistant.TranscriptionModel,
   TranscriptionLanguage = assistant.TranscriptionLanguage,
   // ...
   ```
4. `OpenAiRealtimeAiProviderAdapter.BuildSessionConfig` 在 audio.input / audio.output 構建處**顯式分支**：
   ```csharp
   transcription = new
   {
       model = modelConfig.TranscriptionModel ?? "whisper-1",
       // language 僅當非 null 時才加入物件（避免空字串污染）
   }
   ```

**默認行為保證**（**這是本 PR 的核心**）：
- **Golden payload 測試**：當所有新欄位為 null 時，序列化後的 JSON 字符串必須與 hotfix #934 之後的 byte-equivalent
- 把現有 12 個 `OpenAiRealtimeAiProviderAdapterGaPayloadTests` 全部跑一遍 → 必須全綠
- 新增 1 個專門的 byte-equivalent golden test：
  ```csharp
  [Fact]
  public void BuildSessionConfig_AllConfigNull_ProducesIdenticalJsonToPreFour()
  {
      // 預期 JSON 從 PR 4.2 之前的 git tree 提取出來作為金樣
      var expected = LoadGoldenJsonFromTestData("session-config-defaults-2026-05-12.json");
      var actual = JsonConvert.SerializeObject(adapter.BuildSessionConfig(options, codec));
      actual.ShouldBe(expected);
  }
  ```

**Opt-in 守護**：
- DB 欄位 NULL → adapter helper 走 pre-4.2 fallback 路徑（byte-equivalent）
- DB 欄位非 NULL → 該欄位 per-assistant 生效（其他欄位獨立）
- **不引入 env var** — 安全完全由 NULLABLE 欄位的默認 NULL 提供。新代碼路徑天然「不怕 breaking」：所有現存 prod row 都是 NULL state，與今天輸出 byte-equivalent。

**TDD**：
1. 全 null → byte-equivalent 與 pre-4.2（load-bearing invariant）
2. 各個欄位獨立 activation（per-field）
3. 某一欄位設置時，其他 null 欄位仍走 fallback（隔離性）
4. 全欄位設置 → 全部一起 activate（上界）

**回滾**：`UPDATE ai_speech_assistant SET <field> = NULL WHERE id = <id>` — per-assistant 粒度，不需重啟服務

**驗證信號**：staging 把一個 assistant 設定 `transcription_language='yue'`，跑通話確認 hint 生效；其他 assistants 保持 NULL，行為不變

---

## Phase 5 — 逐個 opt-in（每個 PR 最小化、可獨立回滾）

> **Phase 5 各 PR 嚴格獨立**。一個 PR 即使在 prod 啟用後出問題，也不影響其他 PR 的 opt-in。每個 PR 完整生命週期：dev → staging assistant ID = X 灰度 → 1 prod canary（低流量 assistant）→ 多 prod → 全 prod。

> **Phase 5 內部順序建議**（按穩定性收益遞減）：
> 1. 5.1 transcription language hint — **混合粵/普/英最大收益**
> 2. 5.2 semantic_vad — 減少 filler 誤觸打斷
> 3. 5.3 noise reduction — 通話品質
> 4. 5.4 transcription model upgrade — 成本敏感，後做
> 5. 5.5 max tokens — bound runaway
> 6. 5.6 output speed — UX 調優
> 7. 5.7 voice extension — 純列表擴充

### PR 5.1 — Transcription Language Hint（**最大穩定性收益**）

**動機**：餐廳場景常見粵語 → 普通話 → 英語混講。Whisper-1 多語自動檢測在 utterance 邊界容易切錯（如「兩個 large size 嘅 pepperoni」可能切到 zh-CN 後丟掉 "large size"）。設置 `language: "zh"` hint 把 Whisper 鎖到中文家族，混入英語單詞仍能識別但不切語言。

**修改**：
- `OpenAiRealtimeAiProviderAdapter.BuildSessionConfig` 在 `audio.input.transcription` 構建處：
  ```csharp
  transcription = BuildTranscriptionConfig(modelConfig)

  // 新私有 method
  private static object BuildTranscriptionConfig(RealtimeAiModelConfig cfg)
  {
      var lang = cfg.TranscriptionLanguage;

      if (string.IsNullOrEmpty(lang))
          return new { model = cfg.TranscriptionModel ?? "whisper-1" };

      return new { model = cfg.TranscriptionModel ?? "whisper-1", language = lang };
  }
  ```

**默認行為保證**：
- `cfg.TranscriptionLanguage == null` → payload 中 `transcription` 物件不含 `language` key → byte-equivalent 與當前
- Golden payload test：12 個現有 pinning test 通過 + 新增 `BuildSessionConfig_LanguageNull_NoLanguageKey`

**Opt-in**：
1. DB column `transcription_language` 非 null（如 `"zh"`、`"yue"`、`"en"`）
2. value validation：必須是 ISO-639-1 兩字符代碼或 `"yue"`（Cantonese）；invalid → log warning + 不發 language（fail-soft）

**TDD**：
1. language=null → payload 中無 language key（byte-equivalent）
2. language="zh" → 發出
3. language="yue" → 發出
4. language="zh-CN"（含中橫線，invalid）→ 記 warning + 不發
5. language="" 空字串 → 視同 null

**灰度路徑**：
- staging：取一個 100% Cantonese 場景 assistant，設 `"yue"`
- prod canary：1 個 assistant 設 `"zh"`，觀察 input transcription completeness rate ≥ 同期未啟用 assistant
- prod batch：5 個 assistants，1 週
- prod full：全部

**回滾**：`UPDATE ai_speech_assistant SET transcription_language = NULL WHERE id = <id>` — per-assistant 粒度

**驗證信號**：
- input transcription rate（成功識別的 user utterance / total user utterance）→ ≥ 基線 +2%
- mid-utterance language-switch artifacts（人工抽樣）→ 顯著下降

---

### PR 5.2 — Semantic VAD opt-in

**動機**：`server_vad`（音量門檻）會在用戶說「呃...」「嗯...」這類 filler 時誤判 turn-end → AI 提前接話 → 用戶感覺 AI 「插嘴」。GA 引入 `semantic_vad`（語義邊界判斷）顯著改善。

**修改**：
- `OpenAiRealtimeAiProviderAdapter.BuildSessionConfig` 在 `audio.input.turn_detection` 構建處：
  ```csharp
  turn_detection = BuildTurnDetection(modelConfig)

  private static object BuildTurnDetection(RealtimeAiModelConfig cfg)
  {
      // 顯式 type 配置優先
      if (!string.IsNullOrEmpty(cfg.TurnDetectionType))
      {
          return new
          {
              type = cfg.TurnDetectionType,
              threshold = cfg.TurnDetectionThreshold,           // null 時不發
              silence_duration_ms = cfg.TurnDetectionSilenceMs  // null 時不發
          };
      }

      // 既有路徑（保持兼容）
      return cfg.TurnDetection ?? new { type = "server_vad" };
  }
  ```

**默認行為保證**：
- 當 `TurnDetectionType == null` 時，走原 `cfg.TurnDetection ?? server_vad` 路徑 → byte-equivalent
- Golden test：`BuildSessionConfig_TurnDetectionTypeNull_FallsBackToFunctionCallConfig`

**Opt-in**：
- DB `turn_detection_type` = `"semantic_vad"`（或 `"server_vad"` 顯式）
- value validation：必須 ∈ {`server_vad`, `semantic_vad`}；invalid → log warning + 走原 fallback

**TDD**：6 cases（null fallback / 兩種有效 type / invalid type / threshold + silence_ms 帶入）

**灰度**：
- staging：assistant 用 semantic_vad 跑模擬通話（含 filler）
- prod canary：1 個 assistant，觀察「AI premature interrupt rate」（用戶打斷被觸發後 1s 內又 AI 復原）

**回滾**：`UPDATE ai_speech_assistant SET turn_detection_type = NULL WHERE id = <id>`

**驗證信號**：interrupt false-positive rate 下降

---

### PR 5.3 — Noise Reduction Config

**動機**：手機通話 vs speakerphone vs Bluetooth 噪聲特徵不同。OpenAI GA 提供 `near_field`（手機緊貼耳，低噪）和 `far_field`（speakerphone，環境噪聲多）兩種 NR profile。

**修改**：
- adapter 中：
  ```csharp
  noise_reduction = BuildNoiseReduction(modelConfig)

  private static object BuildNoiseReduction(RealtimeAiModelConfig cfg)
  {
      // 顯式配置優先
      if (!string.IsNullOrEmpty(cfg.InputNoiseReductionType))
          return new { type = cfg.InputNoiseReductionType };

      // 既有路徑
      return cfg.InputAudioNoiseReduction;
  }
  ```

**默認行為保證**：當 `InputNoiseReductionType == null` 時走 `cfg.InputAudioNoiseReduction` 原路徑

**Opt-in**：DB 欄位 `input_noise_reduction_type` 非 null

**value validation**：∈ {`near_field`, `far_field`}；invalid → log warning + 走原 fallback

**TDD**：5 cases（null fallback / 兩種有效 type / invalid → fallback + warning / 空字串視同 null）

**灰度**：先試 `near_field`（多數場景），speakerphone-heavy 餐廳用 `far_field`

**回滾**：`UPDATE ... SET input_noise_reduction_type = NULL WHERE id = <id>`

---

### PR 5.4 — Transcription Model Upgrade

**動機**：whisper-1 已上線多年，gpt-4o-transcribe 是 GA-only 新模型，多語混合準確率高，但**成本 4× 更高**。為 high-value 客戶提供。

**修改**：adapter 中 `transcription.model` 從硬編碼 `"whisper-1"` 改為 `cfg.TranscriptionModel ?? "whisper-1"`

**Opt-in**：DB 欄位 `transcription_model` = `"gpt-4o-transcribe"`

**警告機制**：
- 啟用 `gpt-4o-transcribe` 時，service log warning：「Assistant {id} using premium transcription model, ~4x cost」

**value validation**：∈ {`whisper-1`, `gpt-4o-transcribe`}；invalid → log warning + 走原 fallback (`whisper-1`)

**TDD**：5 cases + cost warning assertion

**灰度**：staging 跑一次完整通話比對 transcription accuracy（人工標註 200 utterance）

**回滾**：`UPDATE ... SET transcription_model = NULL WHERE id = <id>` → whisper-1

---

### PR 5.5 — Max Response Output Tokens

**動機**：當前無 cap，AI 偶爾「滔滔不絕」獨白 30+ 秒，吃 token 又惹用戶反感。

**修改**：adapter session 物件加：
```csharp
max_response_output_tokens = cfg.MaxResponseOutputTokens
// JSON serializer: null 時不發送該 key
```

**默認**：`null` → GA server 默認 unlimited

**Opt-in**：DB `max_response_output_tokens` 設整數（推薦 800-1500）

**value validation**：1-4096

**TDD**：4 cases

---

### PR 5.6 — Audio Output Speed

**動機**：elderly 客戶聽不清快語速，年輕客戶嫌慢。GA 提供 `audio.output.speed` 0.25-1.5。

**修改**：adapter `audio.output` 構建處加 speed 欄位（null 時不發）

**Opt-in**：DB `output_audio_speed` = decimal（推薦 0.85-1.15）

**value validation**：0.25 ≤ value ≤ 1.5

**TDD**：4 cases

---

### PR 5.7 — Voice List Extended

**動機**：現有 `model_voice` 是 string 字段，但 UI 只暴露 6 個。GA 新增 marin / cedar 等。

**修改**：
- 純 enum / validator 擴充：列表加 alloy / ash / ballad / coral / echo / fable / onyx / nova / sage / shimmer / verse / marin / cedar
- UI 開放剩餘選項
- 任何當前 voice 仍合法（向前兼容）

**TDD**：13 cases（每個 voice name 一個）

**回滾**：移除新 voice option（UI 層）

---

## Phase 6 — Function Output 類型化（純內部 refactor）

> **Phase 6 全程**對 OpenAI / Twilio / 用戶完全不可見。所有 PR 都用 **golden JSON payload test** 守護「byte-equivalent JSON 輸出」。

### PR 6.1 — Function Output 抽象基類（不替換）

**動機**：當前 function call output 用 anonymous object → 每次 hot path serialize 走 reflection。引入類型化後可 cache serializer，省 5-10% CPU。

**修改**：純加類，**不替換現有 caller**：
```csharp
public abstract record FunctionCallOutput { /* common fields */ }
public sealed record ConfirmOrderOutput(...) : FunctionCallOutput;
// 暫不修改 caller
```

**默認行為保證**：caller 仍走 anonymous object，零行為變更

**TDD**：抽象基類序列化 vs 同形狀 anonymous object → 兩者 JSON 一致

---

### PR 6.2-6.7 — 逐 Function 切類型

每個 PR 對應一個 function（Hangup / Confirm / RepeatOrder / TransferCall / ...）：
1. 改 caller 用新類型
2. **Golden JSON test**：替換前後 JSON byte-equivalent
3. 獨立可 revert

**默認行為保證**：每個 PR 一個 function，獨立 golden test，無連鎖風險

**TDD**：每 PR 5-10 cases（覆蓋該 function 所有 output 變化）

**灰度**：staging 跑 1 週，prod 一鍵全切（因 byte-equiv）

---

## Phase 7 — Knowledge Build 性能（**最大冷啟動收益**）

> 當前 `BuildKnowledgeAsync` 順序 await 7 個方法，加起來 ~800ms 冷延遲（首次 AI 回應前）。並行化後 ~200ms。

### PR 7.1 — Fetch / Apply 拆分（純 refactor，零行為變更）

**動機**：把每個 `Resolve*Async` 拆成「純讀（fetch）」+「純改 `_ctx`（apply）」兩部分，使後續可並行 fetch。

**修改**（示意）：
```csharp
// Before
private async Task ResolveGreetingAsync(CancellationToken ct)
{
    var greeting = await _someDataProvider.GetGreetingAsync(...);
    _ctx.Prompt = _ctx.Prompt.Replace("{Greeting}", greeting);
}

// After
private async Task<string> FetchGreetingAsync(CancellationToken ct)
    => await _someDataProvider.GetGreetingAsync(...);

private void ApplyGreeting(string greeting)
    => _ctx.Prompt = _ctx.Prompt.Replace("{Greeting}", greeting);

// Pipeline 順序不變：
var greeting = await FetchGreetingAsync(ct);
ApplyGreeting(greeting);
// ...
```

**默認行為保證**：
- 順序仍為 7 個 fetch + 7 個 apply 嚴格串行
- 每個 fetch 純讀，無 side effect
- 每個 apply 純改 `_ctx.Prompt`
- 對外 `BuildKnowledgeAsync` 簽名與行為完全不變

**TDD**：所有現有 prompt-resolution 集成測試通過（行為等價是核心 invariant）

**回滾**：`git revert`

---

### PR 7.2 — 並行 Fetch（純結構替換）

**動機**：基於 7.1，所有 fetch 都是純讀，可 Task.WhenAll 並行。

**修改**：
```csharp
private async Task BuildKnowledgeAsync(CancellationToken ct)
{
    await LoadAssistantInfoAsync(ct);  // 必須先做，後續依賴 _ctx.Assistant

    ResolveStaticPromptVariables();

    // Fetch 全部並行（每個 method 純讀 DB，無 race），Apply 仍順序執行
    var greetingTask       = FetchGreetingAsync(ct);
    var customerItemsTask  = FetchCustomerItemsAsync(ct);
    // ... 6 個並行

    await Task.WhenAll(greetingTask, customerItemsTask, ...);

    // 順序 apply（保證 prompt token 替換順序確定）
    ApplyGreeting(greetingTask.Result);
    ApplyCustomerItems(customerItemsTask.Result);
    // ...
}
```

**默認行為保證**：
- 並行 fetch 結果 = 順序 fetch 結果（純讀 DB，無 side effect，無 race）
- Apply 順序保持與 7.1 相同 → 最終 `_ctx.Prompt` byte-equivalent
- 7.1 已建立 fetch/apply 分離，並沒有引入新的 mutable shared state

**TDD**：
1. Golden：每個 7.1 既有 prompt-resolution test pass（行為等價）
2. 同一 assistant 跑 100 次 → `_ctx.Prompt` 結果穩定（無 race-induced non-determinism）
3. 一個 fetch throw → 整體 ConnectAsync 仍以同樣異常結束（與 sequential 一致）
4. cancellation test：傳入已 cancel token，並行路徑也尊重

**灰度**：
- staging：跑 1 週，量測 p50/p95 BuildKnowledgeAsync 延遲
- prod canary：1 個低流量 assistant 監測（同上）
- prod batch：全 prod

**回滾**：`git revert` — 純結構替換，回滾安全

**驗證信號**：
- p50 BuildKnowledgeAsync 延遲 800ms → < 250ms
- 任何「prompt token 未替換」warning 仍為 0
- 任何 NRE 仍為 0

---

## Phase 8 — Hot-Path Micro-Performance

### PR 8.1 — Prompt Token 單次 Regex

**動機**：當前 `ResolveStaticTokens` + `ResolvePosPromptVariables` 等共做 ~10 次 `string.Replace`，每次 O(n)。改為單次 regex 後 O(n)。

**修改**：
```csharp
private static readonly Regex PromptTokenRegex = new(
    @"\{(\w[\w\d_]*)\}|#\{([\w\d_]+)\}",
    RegexOptions.Compiled);

public static string ApplyTokens(string prompt, Dictionary<string, string> tokens)
{
    return PromptTokenRegex.Replace(prompt, m =>
    {
        var key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        return tokens.TryGetValue(key, out var value) ? value : m.Value;  // 找不到保留原樣
    });
}
```

**默認行為保證**：
- Token map 用同樣 key
- 找不到 token 保留原 placeholder（與當前 `string.Replace` 行為一致 — 找不到不替換）
- **Golden test**：用實際 prompt 範本 + 完整 token map 比對 byte-equivalent

**TDD**：
1. 25 cases（每個 token 一個 + 空 prompt + nested token + escape + 重複 token + 大小寫敏感）
2. Golden replay：取現有 staging 真實 prompt + token map → byte-equivalent

**回滾**：`git revert`

---

### PR 8.2 — Config Deserialize Cache

**動機**：當前每次 ConnectAsync 都 `JsonConvert.DeserializeObject` Tools / TurnDetection 配置，per-assistant 配置很穩定，可 cache。

**修改**：
```csharp
public object DeserializeFunctionCallConfig(int functionCallId, string content)
{
    if (!IsCacheEnabled()) return JsonConvert.DeserializeObject<object>(content);

    return _cache.GetOrCreate($"fc:{functionCallId}:{ContentHash(content)}", entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return JsonConvert.DeserializeObject<object>(content);
    });
}
```

**默認行為保證**：
- Cache key 含 content hash → DB 變更後自動失效（new content → new key → new deserialize）
- TTL 5 min → 兜底失效
- Cache hit / miss 返回值結構與直接 deserialize 完全相同（純記憶化）

**TDD**：
1. cache hit/miss/invalidate 各一
2. content 變更後新 hash → 重新 deserialize
3. 多執行緒並發 GetOrCreate → 同一 key 只執行 deserialize 一次

**灰度**：staging 1 週後 prod canary（量測 ConnectAsync p50 / GC pressure）

**回滾**：`git revert` — 純記憶化，無 DB / API 改動

---

## Phase 9 — GA 觀察 & 收尾

### PR 9.1 — Sunset Beta Event Names（blocked on 2-week obs）

**前提**：hotfix #934 部署後 ≥ 2 週，prod 日誌顯示 OpenAI 完全只發 GA event name（無 `response.audio.delta` 等舊名）。

**修改**：
- V1 + V2 adapter 中 4 個事件的 dual-case 改為單 case：
  ```csharp
  case "response.audio.delta":  // 刪除這行
  case "response.output_audio.delta":  // 保留
  ```
- V1 inline AiSpeechAssistantService 中對應的 `||` 條件改為單名

**默認行為保證**：
- 前提是 OpenAI 已不發舊名 → 刪除舊名分支無 user-visible 影響
- 如果 OpenAI 突然又發舊名（極不可能）→ unknown event，記錄但無實際影響（audio 仍正常）

**TDD**：
1. 現有 dual-name test 改為單 name（assert 新名 → 預期結果，舊名 → unknown）
2. 全 32 個 pinning test 通過

**回滾**：`git revert`（恢復雙 case）

---

### PR 9.2 — `session.tracing=auto` Opt-in

**動機**：OpenAI Realtime API GA 提供官方 tracing — 啟用後在 https://platform.openai.com/traces 可看完整 session（30 天保留）。對排查偶發問題、客訴是神器。

**修改**：
- entity 加 `enable_realtime_tracing` (bool, default false)
- adapter session 物件加 `tracing` 條件欄位（true 時發 `"auto"`，false 時不發）

**默認**：false → 不發 → 與當前 byte-equivalent

**Opt-in**：DB column `enable_realtime_tracing` = true（per-assistant）

**使用場景**：客訴期間單個 assistant 開 tracing → 復現後關閉

**TDD**：3 cases（null/false → 不發；true → 發 `"auto"`）

**回滾**：`UPDATE ... SET enable_realtime_tracing = false WHERE id = <id>`

---

### PR 9.3 — `response.done.usage` 提取與持久化

**動機**：GA 在 `response.done` 帶 usage 物件包含 input_tokens / output_tokens / cached_tokens 等。實時抓取後可
- 通話成本即時計算
- token 異常通話告警
- 每月成本分析

**修改**：
1. V2 adapter `ParseMessage` 對 `response.done` 抽取 usage：
   ```csharp
   case "response.done":
       var usage = TryExtractUsage(root);
       return new ParsedRealtimeAiProviderEvent
       {
           Type = RealtimeAiWssEventType.ResponseTurnCompleted,
           Usage = usage,  // 新欄位
           RawJson = rawMessage
       };
   ```
2. service 端收到後寫入 `AiSpeechAssistantCallReport.TokensUsed`（新欄位）+ `LastResponseUsageJson`

**默認行為保證**：
- 純讀取 + 寫新欄位
- 不影響任何現有事件處理路徑
- 若 usage 缺失（舊模型），fallback 為 null

**TDD**：
1. 5 cases：usage 完整 / 缺失 / 部分欄位 / 數字超大 / 0 token
2. golden：原 `ResponseTurnCompleted` 處理路徑無 regress

**回滾**：`git revert`（新 DB 欄位 NULLABLE，drop 安全）

---

## Phase 10 — Barge-in 正確性（**最高風險，最嚴格灰度**）

> Phase 10 是本輪**唯一直接影響用戶體驗的代碼路徑**。每個 PR 必須有 byte-equivalent 默認路徑 + env flag 啟用新行為。

### PR 10.1 — item_id Tracking（純增量，不影響邏輯）

**動機**：當前 `_aiSpeechAssistantStreamContext.LastAssistantItem` 只記最後一個 item_id，多 audio chunk 場景 truncate 不精確。

**修改**：純增加 tracking，**不改任何邏輯**：
- `_ctx.ActiveAssistantItems` (new field, `Dictionary<string, ItemState>`)
- 對每個 `response.audio.delta`：populate `ActiveAssistantItems[itemId]`
- 對每個 `response.done`：mark item complete
- **不替換** 現有 `LastAssistantItem`

**默認行為保證**：
- 現有 truncate 邏輯仍用 `LastAssistantItem`
- `ActiveAssistantItems` 只記錄不被使用
- 零行為變更

**TDD**：5 cases 驗證 dict 正確 populate

---

### PR 10.2 — Twilio Mark Queue（純增量，不影響邏輯）

**動機**：當前發 audio.delta 給 Twilio 後不知道客戶端實際播了多少。Twilio 支持 mark event 機制：發 mark 後 Twilio 在播完那個 mark 對應位置時回 mark received。

**修改**：純增加機制，**不改任何邏輯**：
- 對每個發給 Twilio 的 audio.delta 同時發一個 mark：`{ event: "mark", streamSid: ..., mark: { name: itemId + "-" + chunkSeq } }`
- 處理 Twilio 端「mark received」回調：更新 `_ctx.PlayedMarks` (new field)

**默認行為保證**：
- mark 是 Twilio 標準支持的 event，不影響音頻播放
- `PlayedMarks` 只記不用
- 零行為變更

**TDD**：4 cases 驗證 mark 發送 + 接收記錄

---

### PR 10.3 — 用 10.1 + 10.2 做精確 Truncate（per-assistant opt-in）

**動機**：基於 10.1 + 10.2 數據，interrupt 時知道客戶端實際聽到了哪個 item 的哪個 chunk → 對 OpenAI 發精確的 `conversation.item.truncate` (audio_end_ms)。

**修改**：
- entity 加 `enable_precise_barge_in` (bool, default false)
- 在 speech_started 處理中加分支：
  ```csharp
  if (_ctx.Assistant.EnablePreciseBargeIn == true)
      HandleSpeechStartedPrecise();
  else
      HandleSpeechStartedLegacy();  // 當前行為
  ```
- `HandleSpeechStartedPrecise` 用 `ActiveAssistantItems` + `PlayedMarks` 計算精確 truncate 點
- 若 precise 路徑遇到數據缺失（如 mark 未收到）→ fallback to legacy（fail-soft）

**默認行為保證**：
- DB 欄位 false / null → 走 legacy（byte-equivalent 與當前）
- 10.1 + 10.2 已將 tracking 數據加上但不用 — 開啟 precise mode 後才消費
- precise 路徑遇缺數據 → fallback to legacy

**TDD**：
1. EnablePreciseBargeIn=false：legacy 路徑（現有 test 全綠）
2. EnablePreciseBargeIn=true + 完整數據：precise 路徑，「用戶在 chunk 3 中打斷」→ truncate 精確到 chunk 3 audio_end_ms
3. EnablePreciseBargeIn=true + 數據缺失：fallback to legacy
4. EnablePreciseBargeIn=true + 無打斷場景：與 legacy byte-equivalent

**灰度**：
- staging：開 1 個 assistant，模擬 barge-in 場景
- prod canary：1 個低流量 assistant 1 週
- 觀察項：
  - AI repeat-after-interrupt 率（用戶打斷後 AI 再說一遍被打斷的句子）→ 應大幅下降
  - 任何 truncate-related 錯誤 → 應為 0
- prod batch / full：分階段

**回滾**：`UPDATE ai_speech_assistant SET enable_precise_barge_in = false WHERE id = <id>` — per-assistant

---

## Phase 11 — Model Migration（最後做，依賴 Phase 5）

### PR 11.1 — gpt-realtime-2 Per-Assistant Override（零風險）

**動機**：`AiSpeechAssistant.ModelUrl` 已支持 per-assistant override。本 PR 純文檔 + UI 開放，**不動 DefaultUrl**。

**修改**：
- UI 開放 ModelUrl 編輯
- 文檔說明 1.5 → 2 升級指引

**默認**：DefaultUrl 仍為 1.5

**Opt-in**：UI 設置單個 assistant 的 ModelUrl 為 `wss://api.openai.com/v1/realtime?model=gpt-realtime-2`

**灰度**：1 個 staging assistant → 1 prod canary 1 週 → 多 prod canary → 條件達成後進入 11.2

**回滾**：UI 清空 ModelUrl → 走 DefaultUrl

---

### PR 11.2 — DefaultUrl Flip（blocked on 11.1 success）

**前提**：11.1 已有 ≥ 5 個 prod assistant 用 gpt-realtime-2 跑 ≥ 1 週無 regression。

**修改**：`AiSpeechAssistantStore.DefaultUrl` 改為 `gpt-realtime-2`

**默認行為保證**（**這是 user-visible 行為變更**）：
- 任何已設 `model_url` 的 assistant 不受影響（per-assistant override 優先）
- 對於沒設 `model_url` 的 assistant，flip 後走 gpt-realtime-2

**緊急回滾路徑**：
- 路徑 A（per-assistant）：對受影響 assistant 顯式設 `model_url = "wss://.../v1/realtime?model=gpt-realtime-1.5"`
- 路徑 B（git revert）：把 DefaultUrl 改回 1.5，下次 deploy 生效

**TDD**：
1. assistant 未設 ModelUrl → 走 gpt-realtime-2
2. assistant 已設 ModelUrl → 用 assistant 的（不受 default 影響）

**灰度**：deploy 後 24h 嚴密觀察；若 regression 廣泛，git revert 比 DB 批量回退更快

---

## 執行順序總覽

```
P0.1 (48h obs) ─┬─► P0.2 (#929 close)
                │
                ├─► Phase 4.1 ─► 4.2 ─┬─► Phase 5.1 (lang hint)  ─┐
                │                      ├─► Phase 5.2 (semantic_vad)│
                │                      ├─► Phase 5.3 (noise)       │
                │                      ├─► Phase 5.4 (model)       ├─► Phase 11.1 ─► 11.2
                │                      ├─► Phase 5.5 (max tokens)  │
                │                      ├─► Phase 5.6 (speed)       │
                │                      └─► Phase 5.7 (voice)       │
                │                                                   │
                ├─► Phase 7.1 ─► 7.2                                │
                ├─► Phase 8.1                                       │
                ├─► Phase 8.2                                       │
                ├─► Phase 6.1 ─► 6.2..6.7                           │
                ├─► Phase 9.2 + 9.3                                 │
                ├─► Phase 10.1 ─► 10.2 ─► 10.3                      │
                │                                                   │
                └─► (2 weeks after hotfix) Phase 9.1                │
```

**並行可能**：Phase 5、6、7、8、9.2、9.3、10.1、10.2 各 PR 之間獨立，可並行進行（每個 PR 有自己分支）。

**強串行**：
- 5.* 需在 4.2 之後
- 7.2 需在 7.1 之後
- 6.2-6.7 需在 6.1 之後
- 10.3 需在 10.1 + 10.2 之後
- 11.2 需在 11.1 灰度成功之後
- 9.1 需 2 週 observation

---

## 每個 PR 提交前自我檢查清單（**強制**）

### 默認行為保證
- [ ] DB 欄位為 NULL → 與當前 prod byte-equivalent
- [ ] Golden JSON / 行為金樣測試已寫
- [ ] 現有所有 unit + integration test 通過

### 安全機制
- [ ] 全 null path 與 pre-PR byte-equivalent 已被 golden test 鎖死
- [ ] invalid 值處理為 fail-soft（log warning + 走 fallback），不 throw
- [ ] 文檔列出「per-assistant SQL UPDATE 回滾」命令

### 代碼品質
- [ ] 方法簽名單行（Rule 1）
- [ ] `.ConfigureAwait(false)` 同行（Rule 2）
- [ ] 方法 ≤ 25 行 / 50 行絕對上限（Rule 3）
- [ ] 主管線扁平（Rule 4）
- [ ] 邏輯塊間有空行（Rule 5）

### 測試
- [ ] Unit + Integration 雙層覆蓋（Rule 9）
- [ ] 4 cases 至少：null → fallback / 單欄位 activate / invalid → fail-soft / 多欄位互不影響

### 文檔
- [ ] PR description 含「默認行為保證」段落
- [ ] PR description 含「per-assistant SQL UPDATE 回滾」一行
- [ ] tracking 表格狀態更新
- [ ] Retrospective 段填寫

### 灰度路徑
- [ ] dev / staging / prod canary / prod batch / prod full 路徑明確
- [ ] 每階段觀察項與門檻量化
- [ ] 灰度監測指標已對應 dashboard

---

## Retrospective（每個 PR 合併後填寫，逐個追加）

> 先留空，每個 PR 完成後追加一個段落。
