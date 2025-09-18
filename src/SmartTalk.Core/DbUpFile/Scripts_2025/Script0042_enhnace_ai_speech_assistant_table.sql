alter table `ai_speech_assistant` add column `wait_interval` int not null default 500;
alter table `ai_speech_assistant` add column `is_transfer_human` tinyint(1) not null default 0;

alter table `ai_speech_assistant_function_call` add column `is_active` tinyint(1) not null default 1;