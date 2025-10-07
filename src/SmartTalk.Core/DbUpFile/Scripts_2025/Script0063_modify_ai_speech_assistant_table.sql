ALTER TABLE `ai_speech_assistant` ADD COLUMN `manual_record_whole_audio` tinyint(1) not null default 0;
ALTER TABLE `ai_speech_assistant` ADD COLUMN `custom_repeat_order_prompt` text null;