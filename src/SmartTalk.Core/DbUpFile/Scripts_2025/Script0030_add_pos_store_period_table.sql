create table if not exists `pos_period`
(
    `id` int primary key auto_increment,
    `store_id` int not null,
    `name` varchar(64) not null,
    `day_of_weeks` varchar(16) not null,
    `start_time` time not null,
    `end_time` time not null,
    `type` int not null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;