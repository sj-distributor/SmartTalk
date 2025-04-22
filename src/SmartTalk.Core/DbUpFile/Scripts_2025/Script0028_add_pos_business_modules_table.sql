create table if not exists `pos_company`
(
    `id` int primary key auto_increment,
    `name` varchar(64) not null,
    `description` varchar(512) null,
    `address` varchar(512) null,
    `status` tinyint(1) not null default 0,
    `created_by` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_company_store`
(
    `id` int primary key auto_increment,
    `company_id` int not null,
    `en_name` varchar(64) not null,
    `zh_name` varchar(64) null,
    `description` varchar(512) null,
    `status` tinyint(1) not null default 0,
    `phone_nums` varchar(64) not null,
    `logo` varchar(1024) null,
    `address` varchar(512) not null,
    `latitude` varchar(16) null,
    `longitude` varchar(16) null,
    `link` varchar(512) null,
    `apple_id` varchar(128) null,
    `app_secret` varchar(512) null,
    `created_by` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_company_store_user`
(
    `id` int primary key auto_increment,
    `user_id` int not null,
    `store_id` int not null,
    `created_by` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;