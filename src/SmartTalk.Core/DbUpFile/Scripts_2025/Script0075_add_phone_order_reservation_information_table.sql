CREATE TABLE if not exists `phone_order_reservation_information`
(
    `id` int auto_increment PRIMARY KEY,
    `record_id` int NOT NULL,
    `reservation_date` VARCHAR(150) null,
    `reservation_time` VARCHAR(150) null,
    `user_name` VARCHAR(255) NULL,
    `party_size` int null,
    `special_requests` varchar(255) null,
    `created_date` datetime(3) NOT NULL
) charset = utf8mb4;