ALTER TABLE `ai_speech_assistant` DROP scenario;
alter table `ai_speech_assistant` add column `agent_id` int not null;