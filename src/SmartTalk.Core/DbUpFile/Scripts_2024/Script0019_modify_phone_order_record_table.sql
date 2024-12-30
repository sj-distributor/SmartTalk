ALTER TABLE phone_order_record ADD COLUMN last_modified_by_name VARCHAR(255);

ALTER TABLE phone_order_record ADD INDEX idx_last_modified_by_name (last_modified_by_name);

UPDATE phone_order_record por JOIN user_account ua ON por.last_modified_by = ua.id SET por.last_modified_by_name = ua.user_name;