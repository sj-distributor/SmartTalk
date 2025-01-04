create table if not exists `restaurant_asterisk`
(
    `id` int auto_increment primary key,
    `restaurant_phone_number` varchar(125) not null,
    `cdr_domain_name` varchar(125) not null,
    `created_date` datetime(3) not null
)charset=utf8mb4;