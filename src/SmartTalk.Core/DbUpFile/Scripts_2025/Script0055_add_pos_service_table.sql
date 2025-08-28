create table if not exists pos_service
(
    `id` int primary key auto_increment,
    `name` varchar(36) not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int not null,
    `last_modified_date` datetime(3) not null
    ) charset=utf8mb4;

alter table pos_company modify column pos_service_id int null;

alter table user_account modify column pos_service_id int null;

alter table agent modify column pos_service_id int null;

alter table role modify column pos_service_id int null;

alter table role modify column user_account_level int null;