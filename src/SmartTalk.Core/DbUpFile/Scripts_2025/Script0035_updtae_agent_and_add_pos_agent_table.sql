alter table `agent` modify column `relate_id` int null;
alter table `agent` add column `is_display` tinyint(1) not null default 1;

create table if not exists pos_agent
(
    `id` int primary key auto_increment,
    `store_id` int not null,
    `agent_id` int not null,
    `created_date` datetime(3) not null
)charset=utf8mb4;