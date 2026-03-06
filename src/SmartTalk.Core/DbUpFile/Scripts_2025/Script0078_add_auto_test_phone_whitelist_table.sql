CREATE TABLE IF NOT EXISTS `auto_test_phone_whitelist` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `assistant_name` VARCHAR(64) NOT NULL,
    `phone_number` VARCHAR(32) NOT NULL UNIQUE,
    `created_at` DATETIME(3) NOT NULL
    ) CHARSET=utf8mb4;