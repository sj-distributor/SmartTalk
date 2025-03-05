create table if not exists ai_speech_assistant_function_call
(
    `id` int primary key auto_increment,
    `assistant_id` int not null,
    `name` varchar(255) not null,
    `content` text not null,
    `created_date` datetime(3) not null
) charset=utf8mb4;