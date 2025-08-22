create table if not exists `pos_menu`
(
    `id` int primary key auto_increment,
    `store_id` int not null,
    `menu_id` varchar(36) not null,
    `names` text not null,
    `time_period` text not null,
    `category_ids` varchar(512) not null,
    `status` tinyint(1) not null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_category`
(
    `id` int primary key auto_increment,
    `store_id` int not null,
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
    `store_id` int not null,
    `category_id` int not null,
    `product_id` varchar(36) not null,
    `names` text not null,
    `price` decimal not null,
    `tax` text not null,
    `category_ids` varchar(512) not null,
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
    `order_no` varchar(16) not null,
    `order_id` varchar(32) null,
    `status` int not null default 10,
    `count` int not null,
    `tax` decimal not null,
    `sub_total` decimal not null,
    `total` decimal not null,
    `type` int not null,
    `items` longtext not null,
    `notes` varchar(128) null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;