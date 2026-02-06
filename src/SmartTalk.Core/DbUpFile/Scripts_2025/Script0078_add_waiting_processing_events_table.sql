create table if not exists `waiting_processing_event` 
(
    `id` int primary key auto_increment,
    `record_id` int not null,
    `agent_id` int not null,
    `task_type` int not null,
    `task_status` int not null,
    `task_source` varchar(150) not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) null
)charset=utf8mb4;