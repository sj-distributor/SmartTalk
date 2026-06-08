CREATE TABLE IF NOT EXISTS `crm_sales_auto_sync_run` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `mode` VARCHAR(32) NOT NULL,
    `is_success` TINYINT(1) NOT NULL DEFAULT 0,
    `total_count` INT NOT NULL DEFAULT 0,
    `created_store_count` INT NOT NULL DEFAULT 0,
    `created_assistant_count` INT NOT NULL DEFAULT 0,
    `created_knowledge_count` INT NOT NULL DEFAULT 0,
    `applied_scene_count` INT NOT NULL DEFAULT 0,
    `warnings_json` VARCHAR(4000) NULL,
    `error_message` VARCHAR(4000) NULL,
    `created_date` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`)
);
