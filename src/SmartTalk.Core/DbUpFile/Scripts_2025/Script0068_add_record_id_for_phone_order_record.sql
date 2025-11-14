ALTER TABLE `phone_order_record` MODIFY COLUMN `order_id` VARCHAR(1024) NULL;
ALTER TABLE `phone_order_record` ADD COLUMN `conversation_text` TEXT NULL;