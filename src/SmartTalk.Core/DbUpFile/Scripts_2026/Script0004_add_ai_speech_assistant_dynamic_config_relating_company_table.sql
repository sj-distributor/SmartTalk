CREATE TABLE IF NOT EXISTS ai_speech_assistant_dynamic_config_relating_company (
    `id` INT NOT NULL AUTO_INCREMENT,
    `config_id` INT NOT NULL,
    `company_id` INT NOT NULL,
    `company_name` VARCHAR(255) NOT NULL,
    PRIMARY KEY (`id`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

ALTER TABLE `company`DROP COLUMN `is_bind_config`;