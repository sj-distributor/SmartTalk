create table if not exists `ai_speech_assistant`
(
    `id` int primary key auto_increment,
    `name` varchar(255) not null,
    `did_number` varchar(32) not null,
    `scenario` int not null,
    `created_date` datetime(3) not null
) charset = utf8mb4;

create table if not exists `ai_speech_assistant_prompt_template`
(
    `id` int primary key auto_increment,
    `assistant_id` int not null,
    `template` text not null,
    `created_date` datetime(3) not null
) charset = utf8mb4;

create table if not exists `ai_speech_assistant_user_profile`
(
    `id` int primary key auto_increment,
    `assistant_id` int not null,
    `caller_number` varchar(32) not null,
    `profile_json` text null,
    `created_date` datetime(3) not null
) charset = utf8mb4;