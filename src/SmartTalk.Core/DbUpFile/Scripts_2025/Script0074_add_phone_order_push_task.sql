CREATE TABLE IF NOT EXISTS `phone_order_push_task`
(
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `record_id` INT NOT NULL,
    `parent_record_id` INT NULL,
    `assistant_id` INT NOT NULL,
    `business_key` VARCHAR(128) NOT NULL,
    `task_type` INT NOT NULL,
    `request_json` LONGTEXT NOT NULL,
    `status` INT NOT NULL,
    `created_at` DATETIME(3) NOT NULL
    ) CHARSET = utf8mb4;

CREATE INDEX idx_record_id ON phone_order_push_task(record_id);

CREATE INDEX idx_parent_record_id ON phone_order_push_task(parent_record_id);

CREATE UNIQUE INDEX uk_record_business ON phone_order_push_task(record_id, business_key);