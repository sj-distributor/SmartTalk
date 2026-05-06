create table if not exists `phone_order_conversation_openai`
(
    `id` int primary key auto_increment,
    `record_id` int not null,
    `question` text not null,
    `answer` text not null,
    `order` int not null default 0,
    `start_time` double null,
    `end_time` double null,
    `created_date` datetime(3) not null
) charset = utf8mb4;

create index `idx_phone_order_conversation_openai_record_id`
    on `phone_order_conversation_openai` (`record_id`);
