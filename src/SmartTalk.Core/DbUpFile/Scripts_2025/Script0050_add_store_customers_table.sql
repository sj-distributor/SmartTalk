create table if not exists `store_customer`
(
    `id` int primary key auto_increment,
    `store_id` int not null,
    `name` varchar(255) not null,
    `phone` varchar(64) not null,
    `address` varchar(512) null,
    `latitude` varchar(16) null,
    `longitude` varchar(16) null,
    `timezone` varchar(64) null,
    `room` varchar(64) null,
    `notes` varchar(512) null,
    `is_deleted` tinyint(1) not null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) null
) charset=utf8mb4;