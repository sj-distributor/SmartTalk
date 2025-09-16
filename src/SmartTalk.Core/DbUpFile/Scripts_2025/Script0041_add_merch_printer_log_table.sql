create table if not exists `merch_printer_log`
(
    `id` char(36) primary key,
    `agent_id` int not null,
    `order_id` char(36) default null,
    `printer_mac` varchar(64) not null,
    `print_log_type` tinyint(1) unsigned not null,
    `message` varchar(255) default null,
    `code` smallint(6) default null,
    `code_description` varchar(255) default null,
    `created_date` datetime(3) not null,
    KEY `idx_merch_id_created_date` (`agent_id`,`created_date`)
) charset=utf8mb4;