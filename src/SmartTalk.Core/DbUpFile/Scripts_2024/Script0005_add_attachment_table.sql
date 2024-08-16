create table if not exists attachment
(
    id int auto_increment
    primary key,
    `uuid` char(36) not null,
    file_url varchar(512) not null,
    file_name varchar(128) not null,
    file_size int not null,
    `origin_file_name` varchar(1024) null,
    `file_path` varchar(1024) null,
    `created_date` datetime(3) not null default now(3)
) charset=utf8mb4;
