create table if not exists `printer_token`
(
    `id` char(36) primary key,
    `printer_mac` varchar(64) unique NOT NULL,
    `token` char(36) NOT NULL,
    `created_date` datetime(3) NOT NULL
) charset=utf8mb4;