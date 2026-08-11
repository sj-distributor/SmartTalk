ALTER TABLE `phone_order_record`
    ADD COLUMN `source_provider` VARCHAR(36) NULL AFTER `is_completed`;
