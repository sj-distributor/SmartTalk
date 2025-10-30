create table if not exists `check_first_sentence_prompt`
(
    `id` int primary key auto_increment,
    `agent_id` int unique not null,
    `prompt` text not null,
    `created_date` datetime(3) not null
) charset = utf8mb4;