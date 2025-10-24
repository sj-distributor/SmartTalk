ALTER TABLE `auto_test_task_record` ADD COLUMN `speech_matics_job_id` varchar(64) null;

ALTER TABLE `auto_test_task_record` ADD COLUMN `is_archived` tinyint(1) not null default 0;