CREATE INDEX `idx_assistant_id`  ON `ai_speech_assistant_knowledge` (`assistant_id`);

CREATE INDEX `idx_is_active` ON `ai_speech_assistant_knowledge` (`is_active`);

CREATE INDEX `idx_assistant_id_is_active`ON `ai_speech_assistant_knowledge` (`assistant_id`, `is_active`);