-- Phase 4.1 of Round 2 V2 stability & perf rollout.
-- All columns NULLABLE; null means "use the same default as today, no behavior change".
-- These columns are only READ when both (a) the column is non-null AND (b) the matching
-- env var (introduced in Phase 4.2) is not 'off'. Until Phase 4.2 ships, this migration
-- is a pure schema add and produces zero behaviour change in production.
--
-- No `AFTER` clauses: column placement in the table layout is cosmetic, and including
-- `AFTER` forces MySQL to use ALGORITHM=COPY (full table rebuild + write lock for the
-- duration of the copy) on 5.7, and prevents ALGORITHM=INSTANT (metadata-only, no
-- table touch) on 8.0+. Without `AFTER`, 8.0+ adds these columns instantly and 5.7
-- can use ALGORITHM=INPLACE. The `ai_speech_assistant` table is config-scale so the
-- impact is bounded either way, but the change is preventive — applies to any future
-- migration this pattern is copied to.
ALTER TABLE ai_speech_assistant
    ADD COLUMN `transcription_model`         VARCHAR(64)    NULL,
    ADD COLUMN `transcription_language`      VARCHAR(8)     NULL,
    ADD COLUMN `turn_detection_type`         VARCHAR(32)    NULL,
    ADD COLUMN `turn_detection_threshold`    DECIMAL(4, 3)  NULL,
    ADD COLUMN `turn_detection_silence_ms`   INT            NULL,
    ADD COLUMN `input_noise_reduction_type`  VARCHAR(32)    NULL,
    ADD COLUMN `max_response_output_tokens`  INT            NULL,
    ADD COLUMN `output_audio_speed`          DECIMAL(3, 2)  NULL;
