create table if not exists `pos_menu`
(
    `id` int primary key auto_increment,
    `store_id` int not null,
    `menu_id` varchar(36) not null,
    `names` text not null,
    `time_periods` longtext not null,
    `category_ids` varchar(512) not null,
    `categories` longtext not null,
    `status` tinyint(1) not null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_category`
(
    `id` int primary key auto_increment,
    `menu_id` int not null,
    `category_id` varchar(36) not null,
    `names` text not null,
    `menu_ids` varchar(512) not null,
    `menu_names` text not null,
    `sort_order` int null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_product`
(
    `id` int primary key auto_increment,
    `category_id` int not null,
    `product_id` varchar(36) not null,
    `names` text not null,
    `price` decimal not null,
    `tax` varchar(16) null,
    `category_ids` varchar(512) not null,
    `category_names` text not null,
    `modifiers` longtext not null,
    `status` tinyint(1) not null,
    `sort_order` int null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_order`
(
    `id` int primary key auto_increment,
    `store_id` int not null,
    `name` varchar(64) not null,
    `phone` varchar(16) not null,
    `address` varchar(512) null,
    `latitude` varchar(16) null,
    `longitude` varchar(16) null,
    `room` varchar(64) null,
    `order_num` varchar(16) not null,
    `status` int not null default 10,
    `count` int not null,
    `tax` varchar(8) not null,
    `sub_total` varchar(16) not null,
    `total` varchar(16) not null,
    `type` int not null,
    `order` longtext not null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;