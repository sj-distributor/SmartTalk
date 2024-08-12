create table if not exists user_account
(
    id int primary key auto_increment,
    issuer int default 0 not null,
    created_on datetime(3) not null,
    modified_on datetime(3) not null,
    uuid varchar(36) not null,
    username varchar(512) charset utf8 not null unique,
    password varchar(128) not null,
    active tinyint(1) default 1 not null
)charset=utf8mb4;

create table if not exists `user_account_api_key`
(
    `id` int primary key auto_increment,
    `user_account_id` int not null,
    `api_key` varchar(128) not null,
    `description` varchar(256) null
)