create table if not exists `pos_store_period`
(
    `id` int primary key auto_increment,
    `store_id` int not null,
    `name` varchar(64) not null,
    `day_of_weeks` int not null,
    `start_time` time not null,
    `end_time` time not null,
    `created_date` datetime(3) not null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;