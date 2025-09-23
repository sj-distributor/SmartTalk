alter table `agent_assistant` drop column `is_display`;
alter table `ai_speech_assistant` add column `is_surface` tinyint(1) not null default 0;

