create table if not exists `message_read_record`
(
    `id` int primary key auto_increment,
    `record_id` int not null,
    `user_id` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
    )charset=utf8mb4;