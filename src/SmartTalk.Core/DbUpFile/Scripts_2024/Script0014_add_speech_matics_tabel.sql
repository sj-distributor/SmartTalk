create table if not exists `speech_matics_key`
(
    `id` int auto_increment primary key,
    `key` varchar(128) not null,
    `status` int not null,
    `last_modified_date` datetime(3) null,
    `created_date` datetime(3) not null
)charset=utf8mb4;