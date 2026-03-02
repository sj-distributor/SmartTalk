CREATE TABLE IF NOT EXISTS `knowledge_copy_related`
(
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `source_knowledge_id` INT NOT NULL,
    `target_knowledge_id` INT NOT NULL,
    `copy_knowledge_points` LONGTEXT NOT NULL,
    `is_sync_update` tinyint(1) not null default 0,
    `related_from` varchar(255) not null,
    `created_date` datetime(3) NOT NULL
    ) CHARSET=utf8mb4;
