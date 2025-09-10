create table if not exists `agent_assistant`
(
    `id` int primary key auto_increment,
    `agent_id` int not null,
    `assistant_id` int not null,
    `is_display` tinyint(1) not null,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
)charset=utf8mb4;

CREATE INDEX idx_agent_id ON agent_assistant (agent_id);
CREATE INDEX idx_assistant_id ON agent_assistant (assistant_id);

INSERT INTO agent_assistant (agent_id, assistant_id, is_display, created_date, last_modified_date)
SELECT asa.agent_id, asa.id, false, NOW(3), Now(3) FROM `ai_speech_assistant` asa;

alter table `ai_speech_assistant` drop column `agent_id`;
alter table `ai_speech_assistant` add column `is_default` tinyint(1) not null default 1;