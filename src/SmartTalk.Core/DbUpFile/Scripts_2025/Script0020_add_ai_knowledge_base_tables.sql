CREATE TABLE IF NOT EXISTS number_pool(
    `id` INT PRIMARY KEY AUTO_INCREMENT,
    `number` VARCHAR(48) NOT NULL,
    `is_used` TINYINT(1) Default 0 NOT NULL,
    `created_date` DATETIME(3) NOT NULL
    )CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS ai_speech_assistant_knowledge( 
    `id` INT PRIMARY KEY AUTO_INCREMENT,
    `assistant_id` INT NOT NULL,
    `json` LONGTEXT NULL,
    `prompt` TEXT NULL,
    `version` VARCHAR(128) NOT NULL,
    `is_active` TINYINT(1) Default 1 NOT NULL,
    `created_date` DATETIME(3) NOT NULL,
    `created_by` VARCHAR(255) NOT NULL
    )CHARSET=utf8mb4;