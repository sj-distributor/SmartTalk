create table if not exists `open_ai_api_key_usage_status`
(
    `id` int primary key auto_increment,
    `index` int not null,
    `using_number` int not null,
    `last_modified_by` int null
    `last_modified_date` datetime(3) null
    ) charset=utf8mb4;