create table if not exists ai_speech_assistant_timer
(
    `id` int primary key auto_increment,
    `assistant_id` int not null,
    `time_span_seconds` int not null,
    `alter_content` text null,
    `created_date` datetime(3) not null
) charset=utf8mb4;