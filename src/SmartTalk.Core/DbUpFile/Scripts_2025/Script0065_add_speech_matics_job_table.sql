create table if not exists `speech_matics_job`
(
    `id` int primary key auto_increment,
    `job_id` varchar(255) not null,
    `scenario` int not null,
    `scenario_record_id` varchar(64) not null,
    `callback_url` varchar(256) null,
    `callback_message` longtext null,
    `created_date` datetime(3) not null,
    
    INDEX `idx_job_id` (`job_id`),
    INDEX `idx_scenario_record_id` (`scenario_record_id`)
) charset = utf8mb4;