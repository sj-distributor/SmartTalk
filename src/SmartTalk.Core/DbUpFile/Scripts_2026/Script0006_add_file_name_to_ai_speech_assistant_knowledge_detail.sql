ALTER TABLE ai_speech_assistant_knowledge_detail
    ADD COLUMN `file_name` VARCHAR(255) NULL AFTER `content`;
