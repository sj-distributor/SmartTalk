create table if not exists restaurant
(
    id int auto_increment primary key,
    `name` varchar(128) not null,
    `created_date` datetime(3) not null default now(3)
) charset=utf8mb4;

create table if not exists restaurant_menu_item
(
    id int auto_increment primary key,
    `restaurant_id` int not null,
    `price` decimal(8,3) null,
    `name_en` varchar(256) not null,
    `name_zh` varchar(256) not null,
    `created_date` datetime(3) not null default now(3)
) charset=utf8mb4;