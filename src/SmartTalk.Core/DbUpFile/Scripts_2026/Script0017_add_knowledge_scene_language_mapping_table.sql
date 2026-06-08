CREATE TABLE IF NOT EXISTS `knowledge_scene_language_mapping` (
    `id` INT NOT NULL AUTO_INCREMENT,
    `scene_id` INT NOT NULL,
    `language` VARCHAR(64) NOT NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME NULL,
    PRIMARY KEY (`id`),
    KEY `idx_knowledge_scene_language_mapping_scene_id` (`scene_id`),
    KEY `idx_knowledge_scene_language_mapping_language` (`language`)
);
