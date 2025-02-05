create table if not exists agent
(
    `id` int primary key auto_increment,
    `relate_id` int not null,
    `type` int not null,
    `created_date` datetime(3) not null
) charset=utf8mb4;