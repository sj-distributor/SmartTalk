CREATE TABLE agent_message_record (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `agent_id` INT NOT NULL,
    `record_id` INT NOT NULL,
    `message_date` DATETIME(3) NOT NULL,
    `message_number` INT NOT NULL DEFAULT 0,
    `created_date` DATETIME(3) NOT NULL,
    `last_modified_date` DATETIME(3) NOT NULL
);