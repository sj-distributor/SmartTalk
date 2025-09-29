create table if not exists hr_interview_setting (
    `id` int auto_increment,
    `welcome` text NOT NULL,
    `end_message` text NOT NULL,
    `session_id` char(36) NOT NULL,
    `created_date` datetime(3) NOT NULL,
    PRIMARY KEY (`id`)
    )charset=utf8mb4;

create table if not exists hr_interview_setting_question (
    `id` int auto_increment,
    `setting_id` int NOT NULL,
    `session_id` char(36) NOT NULL,
    `type` text NOT NULL,
    `question` text NOT NULL,
    `count` int not null,
    `created_date` datetime(3) NOT NULL,
    PRIMARY KEY (`id`)
    )charset=utf8mb4;

create table if not exists hr_interview_session (
    `id` int auto_increment,
    `session_id` char(36) NOT NULL,
    `message` text NOT NULL,
    `file_url` text NOT NULL,
    `question_type` int NOT NULL,  
    `created_date` datetime(3) NOT NULL,
    PRIMARY KEY (`id`)
    )charset=utf8mb4;