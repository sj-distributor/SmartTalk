create table if not exists `agent_transfer_call_config`
(
    `id` int primary key auto_increment,
    `agent_id` int not null,
    `transfer_call_number` varchar(128) not null,
    `service_hours` text not null,
    `priority` int not null default 0,
    `created_date` datetime(3) not null
) charset=utf8mb4;

create index idx_agent_id on `agent_transfer_call_config` (`agent_id`);
