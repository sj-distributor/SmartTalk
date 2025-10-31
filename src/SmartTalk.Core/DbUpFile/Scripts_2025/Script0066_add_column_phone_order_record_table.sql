ALTER TABLE `phone_order_record`
    CHANGE COLUMN `is_outbound` `order_record_type` TINYINT(1) NULL DEFAULT 0;