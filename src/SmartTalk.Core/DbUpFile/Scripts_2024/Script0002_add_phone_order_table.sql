create table if not exists `phone_order_record`
(
    `id` int primary key auto_increment,
    `session_id` varchar(255) not null,
    `restaurant` int not null,
    `tips` varchar(255) not null,
    `url` varchar(255) null,
    `transcription_text` text not null,
    `created_date` datetime(3) not null
) charset = utf8mb4;

create table if not exists `phone_order_conversation`
(
    `id` int primary key auto_increment,
    `record_id` int not null,
    `question` varchar(255) not null,
    `answer` varchar(255) not null,
    `order` int not null default 50,
    `created_date` datetime(3) not null
) charset = utf8mb4;

create table if not exists `phone_order_order_item`
(
    `id` int primary key auto_increment,
    `record_id` int not null,
    `food_name` varchar(255) not null,
    `quantity` int not null,
    `price` double not null,
    `note` varchar(255) null,
    `order_type` int not null,
    `created_date` datetime(3) not null
) charset = utf8mb4;

CREATE INDEX `idx_id` ON `phone_order_record` (id);
CREATE INDEX `idx_id` ON `phone_order_conversation` (id);
CREATE INDEX `idx_id` ON `phone_order_order_item` (id);
CREATE INDEX `idx_record_id` ON `phone_order_conversation` (record_id);
CREATE INDEX `idx_record_id` ON `phone_order_order_item` (record_id);
CREATE INDEX `idx_restuarant` ON `phone_order_record` (restaurant);