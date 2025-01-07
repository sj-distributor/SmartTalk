create table if not exists `sip_host_server`
(
    `id` int auto_increment primary key,
    `user_name` varchar(255) not null,
    `server_ip` varchar(255) not null,
    `source_path` varchar(1024) null,
    `created_date` datetime(3) not null,
    `last_modified_date` datetime(3) null
)charset=utf8mb4;

create table if not exists `sip_backup_server`
(
    `id` int auto_increment primary key,
    `host_id` int not null,
    `user_name` varchar(255) not null,
    `server_ip` varchar(255) not null,
    `destination_path` varchar(1024) null,
    `exclude_files` varchar(2048) null,
    `status` int not null,
    `created_date` datetime(3) not null,
    `last_modified_date` datetime(3) null
)charset=utf8mb4;