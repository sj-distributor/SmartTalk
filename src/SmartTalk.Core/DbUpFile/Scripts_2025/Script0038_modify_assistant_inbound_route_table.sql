ALTER TABLE `ai_speech_assistant_inbound_route` ADD COLUMN `timezone` VARCHAR(64) NOT NULL DEFAULT 'Pacific Standard Time';
ALTER TABLE `ai_speech_assistant_inbound_route` ADD COLUMN `day_of_week` VARCHAR(24) NOT NULL;
ALTER TABLE `ai_speech_assistant_inbound_route` ADD COLUMN `start_time` time NULL;
ALTER TABLE `ai_speech_assistant_inbound_route` ADD COLUMN `end_time` time NULL;
ALTER TABLE `ai_speech_assistant_inbound_route` ADD COLUMN `forward_number` VARCHAR(64) NULL;
ALTER TABLE `ai_speech_assistant_inbound_route` ADD COLUMN `priority` int NOT NULL;
ALTER TABLE `ai_speech_assistant_inbound_route` ADD COLUMN `is_full_day` TINYINT(1) NOT NULL DEFAULT 0;

ALTER TABLE `ai_speech_assistant_inbound_route` CHANGE assistant_id forward_assistant_id int NULL;