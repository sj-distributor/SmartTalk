create table if not exists `ai_speech_assistant_inbound_route`
(
    `id` int primary key auto_increment,
    `assistant_id` int not null,
    `from` varchar(48) not null,
    `to` varchar(48) not null,
    `created_date` datetime(3) not null
)charset=utf8mb4;