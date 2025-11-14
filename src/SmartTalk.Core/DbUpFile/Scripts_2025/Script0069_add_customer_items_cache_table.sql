create table if not exists `customer_items_cache`
(
    `id` int auto_increment PRIMARY KEY,
    `cache_key` varchar(255) NOT NULL,
    `cache_value` longtext NOT NULL,
    `last_updated` datetime(3) NOT NULL
    ) charset = utf8mb4;

CREATE INDEX `idx_cache_key` ON `customer_items_cache` (cache_key);p