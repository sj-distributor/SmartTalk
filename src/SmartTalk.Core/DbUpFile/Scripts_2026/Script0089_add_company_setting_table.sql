CREATE TABLE IF NOT EXISTS company_setting (
    `id` INT NOT NULL AUTO_INCREMENT,
    `company_id` INT NOT NULL,
    `setting_key` VARCHAR(128) NOT NULL,
    `setting_value` VARCHAR(255) NULL,
    `created_date` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `last_modified_date` DATETIME NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_company_setting_key` (`company_id`, `setting_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;