ALTER TABLE ai_speech_assistant
    CHANGE COLUMN did_number answering_number VARCHAR(48) NULL,
    CHANGE COLUMN url model_url VARCHAR(255) NULL,
    CHANGE COLUMN voice model_voice VARCHAR(36) NULL,
    CHANGE COLUMN provider model_provider INT NOT NULL,
    ADD COLUMN answering_number_id INT NOT NULL,
    ADD COLUMN created_by INT NOT NULL;

ALTER TABLE ai_speech_assistant DROP COLUMN greetings, DROP COLUMN custom_record_analyze_prompt;