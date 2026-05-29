CREATE TABLE IF NOT EXISTS ai_speech_assistant_dynamic_config (
  `id` INT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(128) NOT NULL,
  `level` TINYINT NOT NULL,
  `parent_id` INT NULL,
  `status` TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `idx_dynamic_config_level` (`level`),
  KEY `idx_dynamic_config_parent_id` (`parent_id`),
  KEY `idx_dynamic_config_status` (`status`),
  CONSTRAINT `fk_dynamic_config_parent_id`
      FOREIGN KEY (`parent_id`) REFERENCES `ai_speech_assistant_dynamic_config` (`id`)
      ON DELETE CASCADE
      ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

ALTER TABLE `company`
    ADD COLUMN `is_bind_config` TINYINT(1) NOT NULL DEFAULT 0;
