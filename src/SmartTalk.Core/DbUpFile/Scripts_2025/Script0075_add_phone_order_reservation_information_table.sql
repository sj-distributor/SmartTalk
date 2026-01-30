CREATE TABLE if not exists `phone_order_reservation_information`
(
    `id` int auto_increment PRIMARY KEY,
    `record_id` int NOT NULL,
    `notification_info` text not null,
    `ai_notification_info` text not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) null,
    `created_date` datetime(3) NOT NULL
) charset = utf8mb4;