create table if not exists `auto_test_call_record_sync`
(
    `id` int auto_increment primary key,
    `call_log_id` varchar(64) not null,
    `call_id` varchar(64) not null,
    `from_number` varchar(32) not null,
    `to_number` varchar(32) not null,
    `direction` varchar(16) not null,
    `extension_id` varchar(32) null,
    `start_time_utc` datetime not null,
    `recording_url` longtext null,
    `source` tinyint not null,
    `last_updated` datetime(3) not null,
    unique key `uk_call_log_source` (`call_log_id`, `source`),
    key `idx_start_time` (`start_time_utc`),
    key `idx_from_to` (`from_number`, `to_number`)
    ) charset = utf8mb4;