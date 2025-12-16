CREATE TABLE IF NOT EXISTS `knowledge_copy_related`
(
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `source_knowledge_id` INT NOT NULL,
    `target_knowledge_id` INT NOT NULL,
    `copy_knowledge_points` LONGTEXT NOT NULL,
    `is_sync_update` TINYINT(1) NULL DEFAULT 0,
    `related_from` VARCHAR(1024) NULL,
    `created_date` DATETIME(3) NOT NULL
    ) CHARSET=utf8mb4;
