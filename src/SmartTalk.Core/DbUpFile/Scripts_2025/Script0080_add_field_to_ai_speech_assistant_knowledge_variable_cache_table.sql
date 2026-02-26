ALTER TABLE `ai_speech_assistant_knowledge_variable_cache` ADD COLUMN `system_name` varchar(64) NULL;

ALTER TABLE `ai_speech_assistant_knowledge_variable_cache` ADD COLUMN `category_name` varchar(64) NULL;

ALTER TABLE `ai_speech_assistant_knowledge_variable_cache` ADD COLUMN `field_name` varchar(64) NULL;

ALTER TABLE `ai_speech_assistant_knowledge_variable_cache` ADD COLUMN `level_type` int NOT NULL;

ALTER TABLE `ai_speech_assistant_knowledge_variable_cache` ADD COLUMN `parent_id` int NULL;

ALTER TABLE `ai_speech_assistant_knowledge_variable_cache` ADD COLUMN `is_enabled` tinyint(1) NOT NULL DEFAULT 0;