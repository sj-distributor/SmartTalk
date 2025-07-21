create table if not exists sales
(
    `id` int primary key auto_increment,
    `name` varchar(255) not null,
    `type` int not null,
    `created_by` int null,
    `created_date` datetime(3) not null
) charset=utf8mb4;