alter table `ai_speech_assistant` add column `custom_record_analyze_prompt` text null;
alter table `agent` add column `source_system` int not null default 0;