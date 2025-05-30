CREATE TABLE IF NOT EXISTS ai_kid
(
    `id` int primary key auto_increment,
    `agent_id` int not null,
    `kid_uuid` char(36) not null,
    `created_date` datetime(3) not null
)charset=utf8mb4;