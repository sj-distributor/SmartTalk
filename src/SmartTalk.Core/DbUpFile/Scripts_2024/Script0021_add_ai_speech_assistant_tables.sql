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

CREATE INDEX idx_did_number ON ai_speech_assistant (did_number);
CREATE INDEX idx_assistant_id ON ai_speech_assistant_prompt_template (assistant_id);
CREATE INDEX idx_caller_number ON ai_speech_assistant_user_profile (caller_number);
CREATE INDEX idx_assistant_id ON ai_speech_assistant_user_profile (assistant_id);