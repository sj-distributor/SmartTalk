ALTER TABLE ai_speech_assistant_knowledge_scene_relation
    ADD COLUMN IF NOT EXISTS `source_type` int NOT NULL DEFAULT 0;
