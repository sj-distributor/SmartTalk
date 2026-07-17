ALTER TABLE ai_speech_assistant_knowledge_detail
    ADD COLUMN `source_type` VARCHAR(32) NULL,
    ADD COLUMN `source_scene_id` INT NULL,
    ADD COLUMN `source_scene_item_id` INT NULL;
