CREATE TABLE IF NOT EXISTS ai_speech_assistant_knowledge_detail (
  `id` INT NOT NULL AUTO_INCREMENT,
  `knowledge_id` INT NOT NULL,
  `knowledge_name` VARCHAR(255) NOT NULL,
  `format_type` INT NOT NULL,
  `content` TEXT NOT NULL,
  `created_date` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `last_modified_by` INT NULL,
  `last_modified_date` DATETIME(6) NULL DEFAULT NULL,
  PRIMARY KEY (`id`),
  INDEX `idx_knowledge_id` (`knowledge_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;