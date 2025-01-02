ALTER TABLE phone_order_record ADD COLUMN last_modified_by_name VARCHAR(255);

CREATE INDEX `idx_last_modified_by_name` ON `phone_order_record` (last_modified_by_name);