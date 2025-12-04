create table if not exists hr_interview_question
(
    `id` int primary key auto_increment,
    `section` int not null,
    `question` text not null,
    `is_using` TINYINT(1) Default 0 NOT NULL,
    `created_date` datetime(3) not null
) charset=utf8mb4;