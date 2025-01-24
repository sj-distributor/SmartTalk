create table if not exists ai_speech_assistant_human_contact
(
    `id` int primary key auto_increment,
    `assistant_id` int not null,
    `human_phone` varchayr(36) not null,
    `created_date` datetime(3) not null
) charset=utf8mb4;

CREATE INDEX idx_assistant_id ON ai_speech_assistant_human_contact (assistant_id);