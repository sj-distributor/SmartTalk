ALTER TABLE `phone_order_record_report` DROP COLUMN `is_customer_friendly`;

ALTER TABLE `phone_order_record` ADD COLUMN `is_customer_friendly` TINYINT(1) NULL DEFAULT 0;

ALTER TABLE `phone_order_record` ADD COLUMN `is_human_answered` TINYINT(1) NULL DEFAULT 0;
