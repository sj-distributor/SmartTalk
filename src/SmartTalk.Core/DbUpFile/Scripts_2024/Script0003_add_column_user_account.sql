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