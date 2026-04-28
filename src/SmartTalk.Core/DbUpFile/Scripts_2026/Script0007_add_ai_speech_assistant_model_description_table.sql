CREATE TABLE IF NOT EXISTS ai_speech_assistant_description (
  `model_id` VARCHAR(64) NOT NULL,
  `model_description` VARCHAR(1024) NULL,
  `model_value` VARCHAR(512) NOT NULL,
  PRIMARY KEY (`model_id`),
  KEY `idx_ai_speech_assistant_description_model_value` (`model_value`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
