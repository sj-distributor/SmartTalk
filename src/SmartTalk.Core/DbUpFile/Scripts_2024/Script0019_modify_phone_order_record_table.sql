ALTER TABLE phone_order_record
    ADD COLUMN last_modified_by_name VARCHAR(255),
  ADD INDEX idx_last_modified_by_name (last_modified_by_name);