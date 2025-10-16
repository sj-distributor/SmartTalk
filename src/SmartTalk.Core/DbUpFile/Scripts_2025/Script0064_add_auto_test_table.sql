create table if not exists auto_test_scenario
(
    `id` int primary key auto_increment,
    `key_name` varchar(128) unique null,
    `name` varchar(256) null,
    `input_schema` text not null,
    `output_schema` text not null,
    `action_config` text not null,
    `created_at` dateTime(3) not null,
    `updated_at` dateTime(3) null
    ) charset=utf8mb4;

create table if not exists auto_test_import_data_record
(
    `id` int primary key auto_increment,
    `scenario_id` int not null,
    `type` int not null,
    `op_config` text not null,
    `status` int not null default 0,
    `created_at` dateTime(3) not null, 
    `started_at` dateTime(3) null,
    `finished_at` dateTime(3) null
    ) charset=utf8mb4;

create table if not exists auto_test_data_set
(
    `id` int primary key auto_increment,
    `scenario_id` int not null,
    `key_name` varchar(128) unique null,
    `name` varchar(256) null,
    `created_at` dateTime(3) not null
    ) charset=utf8mb4;

create table if not exists auto_test_data_item
(
    `id` int primary key auto_increment,
    `scenario_id` int not null,
    `import_record_id` int not null, 
    `input_json` text not null, 
    `quality` text null,                
    `created_at` dateTime(3) not null
    ) charset=utf8mb4;

create table if not exists auto_test_data_set_item
(
    `id` int primary key auto_increment,
    `data_set_id` int not null,
    `data_item_id` int not null,
    `created_at` dateTime(3) not null
    ) charset=utf8mb4;

create table if not exists auto_test_test_task
(
    `id` int primary key auto_increment,
    `scenario_id` int not null,
    `data_set_id` int not null,  
    `params` text null,
    `status` int not null,
    `created_at` dateTime(3) not null,
    `started_at` dateTime(3) null,
    `finished_at` dateTime(3) null
    ) charset=utf8mb4;

create table if not exists auto_test_test_task_record
(
    `id` int primary key auto_increment,
    `test_task_id` int not null,
    `scenario_id` int not null,
    `data_set_id` int not null,  
    `data_set_item_id` int not null, 
    `input_snapshot` text not null,
    `request_json` text null, 
    `raw_output` text null,
    `normalized_output` text null,
    `evaluation_summary` text null,
    `validation_errors` text null,
    `status` int not null,
    `error_text` text null,
    `created_at` dateTime(3) not null
    ) charset=utf8mb4;