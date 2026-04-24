create table if not exists `ai_speech_assistant_premise`
(
    `id` int auto_increment PRIMARY KEY,
    `assistant_id` int NOT NULL,
    `content` longtext NOT NULL,
    `created_date` datetime(3) NOT NULL
) charset = utf8mb4;

CREATE INDEX `idx_assistant_id` ON `ai_speech_assistant_premise` (assistant_id);