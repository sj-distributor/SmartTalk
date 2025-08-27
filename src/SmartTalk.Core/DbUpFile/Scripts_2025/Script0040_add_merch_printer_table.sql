create table if not exists `merch_printer`
(
    `id` int primary key auto_increment,
    `agent_id` int unique not null,
    `printer_name` varchar(255) NOT NULL DEFAULT '',
    `printer_mac` varchar(255) unique NOT NULL,
    `status_info` varchar(500) DEFAULT NULL,
    `is_enabled` tinyint(1) unsigned NOT NULL DEFAULT '0',
    `token` char(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    `status_info_last_modified_date` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
) charset=utf8mb4;

create table if not exists `merch_printer_order`
(
    `id` char(36) primary key,
    `agent_id` int not null,
    `order_id` char(36) not null,
    `print_status` int not null,
    `print_date` datetime(3) NOT NULL,
    `print_error_times` int(11) DEFAULT NULL,
    `created_date` datetime(3) NOT NULL,
    `image_url` varchar(500) DEFAULT NULL,
    `image_key` varchar(200) DEFAULT NULL,
    `printer_mac` varchar(255) DEFAULT NULL,
    KEY `idx_agent_id_print_status` (`agent_id`,`print_status`),
    KEY `idx_print_date` (`print_date`)
) charset=utf8mb4;