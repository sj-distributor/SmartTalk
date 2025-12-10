alter table `phone_order_record` drop column `update_scenario_user_id`;

create table if not exists `phone_order_record_scenario_history`
(
    `id` int auto_increment PRIMARY KEY,
    `record_id` int NOT NULL,
    `scenario` int NOT null,
    `update_scenario_user_id` int NOT NULL,
    `username` VARCHAR(255) NULL,
    `created_date` datetime(3) NOT NULL
    ) charset = utf8mb4;
