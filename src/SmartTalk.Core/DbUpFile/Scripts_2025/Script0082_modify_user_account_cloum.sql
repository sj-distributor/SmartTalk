ALTER TABLE `user_account`MODIFY COLUMN `is_task_enabled` tinyint(1) not null default 1;
ALTER TABLE `user_account`MODIFY COLUMN `is_turn_on_notification` tinyint(1) not null default 1;