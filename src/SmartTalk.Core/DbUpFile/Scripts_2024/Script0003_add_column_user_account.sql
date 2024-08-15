alter table `user_account` add column `third_party_user_id` varchar(128) null;

CREATE UNIQUE INDEX idx_third_party_user_id ON user_account(third_party_user_id);

create table if not exists user_account_profile(
    `id` int auto_increment,
    `user_account_id` int NOT NULL,
    `created_date` datetime(3) NOT NULL,
    `display_name` varchar(512) NULL,
    `phone` varchar(50) NULL,
    `email` varchar(128) NULL,
    PRIMARY KEY (`id`),
    UNIQUE INDEX `uq_user_account_id` (`user_account_id`)
)charset=utf8mb4;

CREATE INDEX idx_user_account_id ON user_account_profile (user_account_id);

create table if not exists verification_code(
    `id` int auto_increment,
    `user_account_id` int NULL,
    `identity` varchar(128) NULL,
    `created_date` datetime(3) NOT NULL,
    `expired_date` datetime(3) NOT NULL,
    `authenticated_date` datetime(3) NULL,
    `code` varchar(64) NOT NULL,
    `recipient` varchar(128) NOT NULL,
    `verification_method` int NOT NULL,
    `failed_attempts` int NOT NULL default 0,
    `authentication_status` int NOT NULL default 10,
    PRIMARY KEY (`id`)
)charset=utf8mb4;

CREATE UNIQUE INDEX uq_user_status ON verification_code (user_account_id, authentication_status);

create table if not exists `role`
(
    id int auto_increment primary key,
    created_on datetime(3) not null,
    modified_on datetime(3) not null,
    uuid varchar(36) not null,
    name varchar(512) charset utf8 not null,
    `description` varchar(512) null,
    `display_name` varchar(255) null,
    `is_system` bit default 0,
    `system_source` int not null default 0,
    constraint idx_name unique (name)
) charset=utf8mb4;

create table if not exists `role_user`
(
    id int auto_increment primary key,
    created_on datetime(3) not null,
    modified_on datetime(3) not null,
    uuid varchar(36) not null,
    role_id int not null,
    user_id int not null,
    constraint idx_user_id_role_id unique (user_id, role_id)
) charset=utf8mb4;

create table if not exists `permission` (
    `id` int auto_increment primary key,
    `created_date` datetime(3) not null,
    `last_modified_date` datetime(3) not null,
    `name` varchar(256) not null,
    `description` varchar(512) null,
    `display_name` varchar(255) null,
    `is_system` bit not null,
    unique key `idx_name` (`name`)
) charset=utf8mb4;

create table if not exists `role_permission` (
    `id` int auto_increment primary key,
    `created_date` datetime(3) not null,
    `last_modified_date` datetime(3) not null,
    `role_id` int not null,
    `permission_id` int not null,
    unique key `uq_role_and_permission` (`role_id`,`permission_id`)
) charset=utf8mb4;

create table if not exists `user_permission` (
    `id` int auto_increment primary key,
    `created_date` datetime(3) not null,
    `last_modified_date` datetime(3) not null,
    `user_id` int not null,
    `permission_id` int not null,
    unique key `uq_role_and_permission` (`user_id`,`permission_id`)
) charset=utf8mb4;

CREATE INDEX idx_user_id ON user_permission (user_id);