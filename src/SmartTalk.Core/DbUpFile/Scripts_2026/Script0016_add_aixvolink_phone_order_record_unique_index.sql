CREATE UNIQUE INDEX `idx_aixvolink_phone_order_record_url`
    ON `phone_order_record` (`source_provider`, `url`);
