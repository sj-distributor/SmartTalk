alter table `user_account` add column `is_task_enabled` tinyint(1) not null default 0;
alter table `user_account` add column `is_turn_on_notification` tinyint(1) not null default 0;