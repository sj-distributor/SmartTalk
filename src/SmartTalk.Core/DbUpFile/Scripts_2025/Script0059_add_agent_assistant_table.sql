create table if not exists `agent_assistant`
(
    `id` int primary key auto_increment,
    `agent_id` int not null,
    `assistant_id` int not null,
    `is_default` tinyint not null,
    `is_multiple` tinyint(1) not null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

CREATE INDEX idx_agent_id ON agent_assistant (agent_id);
CREATE INDEX idx_assistant_id ON agent_assistant (assistant_id);