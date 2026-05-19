# V2 Rollout Playbook — Feature Flags & Env Vars

> 所有 V2 改造引入的 env var 集中於此。每行：env var → 引入 PR → 預設值 → 灰度狀態 → 緊急回滾命令。

## 環境變數總覽

| Env Var | 引入 PR | 預設值 | 當前狀態 | 說明 |
|---|---|---|---|---|
| `SQUID_SMARTTALK_REALTIME_WS_KEEPALIVE_SECONDS` | PR 2.3 | `15` | default | 提供 OpenAI/Google realtime WebSocket 客戶端的 keep-alive 間隔（秒）。範圍 5–120；越界或非數字回退為 15s。比 .NET 默認 30s 更頻繁，避免 corporate proxy / cloud LB 在 AI 沉默期間 idle-out 連線。 |
| `SQUID_SMARTTALK_RECORDING_BUFFER_MODE` | PR 3.2 | `unbounded` | default | 錄音 buffer 實現策略。值：`unbounded`（與當前行為一致）或 `rolling`（環形 buffer，超出窗口的舊數據自動丟棄）。任何非 `rolling` 值（含未設）視為 unbounded。 |
| `SQUID_SMARTTALK_RECORDING_BUFFER_SECONDS` | PR 3.2 | `300` | default | 當 `BUFFER_MODE=rolling` 時的窗口大小（秒）。範圍 30–3600；越界回退 300。300s × 48,000 byte/s = 14.4 MB cap per call。 |

## Round 2 — per-assistant config（無 env var）

Phase 4.2 起的 Realtime API session-config overrides **不引入 env var**：activation 完全由 DB 欄位驅動。

- 每個 override 欄位（`transcription_language`、`turn_detection_type`、`output_audio_speed` 等）在 `ai_speech_assistant` 表為 NULLABLE
- NULL → 走 pre-4.2 默認分支（byte-equivalent 與當前 prod 一致）
- 非 NULL → 該欄位 per-assistant 生效（其他欄位獨立）
- 緊急回滾：`UPDATE ai_speech_assistant SET <field> = NULL WHERE id = <assistant_id>` — 不需要重啟服務，per-assistant 粒度

## 灰度狀態定義

- `default` — 默認值，與線上行為一致
- `staging-warn` — Staging 上 warn 模式（行為改變但記錄日誌）
- `staging-strict` — Staging 上 strict 模式
- `prod-canary` — 生產灰度單個 assistant
- `prod-batch` — 生產分批啟用
- `prod-full` — 生產全量

## 緊急回滾矩陣

| 場景 | 操作 |
|---|---|
| 通話質量下降 | 找出可能的 env var → set `=off` → 重啟服務 |
| OpenAI 錯誤率上升 | 同上 + 檢查 OpenAI status |
| Twilio 媒體流失敗 | 重點檢查 audio format / voice 相關 flag |

## 每個 PR 引入新 flag 時的記錄要求

引入新 flag 時必須：
1. 在「環境變數總覽」加一行
2. 在 PR description 註明 default 與 rollout plan
3. 確認 unit test pinning env var name (Rule 8)
4. 在 retrospective 記錄首次啟用時間
