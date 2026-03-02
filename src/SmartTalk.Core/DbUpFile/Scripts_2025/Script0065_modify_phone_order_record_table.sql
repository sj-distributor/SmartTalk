ALTER TABLE `phone_order_record` ADD COLUMN `is_outbound` tinyint(1) null default 0;

ALTER TABLE `phone_order_record_report` ADD COLUMN `is_customer_friendly` tinyint(1) null default 0;