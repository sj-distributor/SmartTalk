alter table `ai_speech_assistant` modify column `is_complaint_analysis_enabled` tinyint(1) null default null;
update `ai_speech_assistant` set `is_complaint_analysis_enabled` = null;
