ALTER TABLE knowledge_copy_related DROP COLUMN source_knowledge_name;

ALTER TABLE knowledge_copy_related DROP COLUMN is_sync_update;

ALTER TABLE knowledge_copy_related DROP COLUMN related_from;

ALTER TABLE `ai_speech_assistant_knowledge` ADD COLUMN `is_sync_update` TINYINT(1) NULL DEFAULT 0;
