RENAME TABLE `customer_items_cache` TO `ai_speech_assistant_knowledge_variable_cache`;

ALTER TABLE `ai_speech_assistant_knowledge_variable_cache` ADD COLUMN `filter` VARCHAR(255) NOT NULL;