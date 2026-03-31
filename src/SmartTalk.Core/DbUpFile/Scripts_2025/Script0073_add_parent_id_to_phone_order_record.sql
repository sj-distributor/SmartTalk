alter table `phone_order_record` add column `parent_record_id` int null;
alter table `phone_order_record` add column `is_completed` tinyint(1) not null default 0;