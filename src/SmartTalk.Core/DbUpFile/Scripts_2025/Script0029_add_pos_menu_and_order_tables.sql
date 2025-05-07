create table if not exists `pos_menu`
(
    `id` int primary key auto_increment,
    `store_id` int not null,
    `menu_id` char(36) not null,
    `name_zh` varchar(36) not null,
    `name_en` varchar(36) not null,
    `pos_name_zh` varchar(36) not null,
    `pos_name_en` varchar(36) not null,
    `start_time` varchar(16) not null,
    `end_time` varchar(16) not null,
    `opening_hours_name` varchar(36) not null,
    `category_ids` varchar(512) not null,
    `category_names_zh` varchar(1024) not null,
    `category_names_en` varchar(1024) not null,
    `status` tinyint(1) not null,
    `created_by` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_category`
(
    `id` int primary key auto_increment,
    `menu_id` int not null,
    `category_id` char(36) not null,
    `name_zh` varchar(36) not null,
    `name_en` varchar(36) not null,
    `pos_name_zh` varchar(36) not null,
    `pos_name_en` varchar(36) not null,
    `menu_ids` varchar(512) not null,
    `menu_names_zh` varchar(1024) not null,
    `menu_names_en` varchar(1024) not null,
    `sort_order` int not null default 1,
    `created_by` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_product`
(
    `id` int primary key auto_increment,
    `category_id` int not null,
    `product_id` char(36) not null,
    `name_zh` varchar(36) not null,
    `name_en` varchar(36) not null,
    `pos_name_zh` varchar(36) not null,
    `pos_name_en` varchar(36) not null,
    `price` double not null,
    `tax` varchar(8) not null,
    `category_ids` varchar(512) not null,
    `category_names_zh` varchar(1024) not null,
    `category_names_en` varchar(1024) not null,
    `modifiers` text not null,
    `modifier_ids` varchar(512) not null,
    `modifier_names_zh` varchar(1024) not null,
    `modifier_names_en` varchar(1024) not null,
    `status` tinyint(1) not null,
    `sort_order` int not null default 1,
    `created_by` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
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
    `tax` double not null,
    `count` varchar(16) not null,
    `sub_total` varchar(16) not null,
    `total` varchar(16) not null,
    `type` int not null,
    `note` varchar(512) null,
    `created_by` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

create table if not exists `pos_order_item`
(
    `id` int primary key auto_increment,
    `order_id` int not null,
    `product_id` int not null,
    `type` int not null,
    `created_by` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;