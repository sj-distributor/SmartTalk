create table if not exists ai_speech_assistant_session
(
    `id` int primary key auto_increment,
    `assistant_id` int not null,
    `session_id` char(32) not null,
    `count` int not null,
    `created_date` datetime(3) not null
) charset=utf8mb4;