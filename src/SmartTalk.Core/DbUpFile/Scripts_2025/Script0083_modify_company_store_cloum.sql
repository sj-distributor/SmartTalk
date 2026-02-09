ALTER TABLE `company_store` add COLUMN `is_task_enabled` tinyint(1) not null default 1;

ALTER TABLE user_account DROP COLUMN is_task_enabled;
