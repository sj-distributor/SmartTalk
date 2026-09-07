create table `call_notification_scenario_rule`
(
    `id`                 int           not null auto_increment,
    `scenario_key`       varchar(64)   not null,
    `description`        varchar(1024) not null,
    `prompt_template`    text          not null,
    `is_active`          tinyint(1)    not null default 1,
    `created_date`       datetime(6)   not null,
    `last_modified_date` datetime(6)   null,
    primary key (`id`),
    unique key `uk_call_notification_scenario_rule_key` (`scenario_key`)
);

create table `store_call_notification_setting`
(
    `id`                 int         not null auto_increment,
    `company_id`         int         not null,
    `store_id`           int         not null,
    `scenario_key`       varchar(64) not null,
    `is_enabled`         tinyint(1)  not null default 0,
    `created_date`       datetime(6) not null,
    `last_modified_date` datetime(6) null,
    primary key (`id`),
    unique key `uk_store_call_notification_setting` (`store_id`, `scenario_key`),
    key `idx_store_call_notification_company` (`company_id`)
);

create table `call_notification_record`
(
    `id`                    int           not null auto_increment,
    `phone_order_record_id` int           not null,
    `company_id`            int           not null,
    `store_id`              int           not null,
    `agent_id`              int           not null,
    `scenario_key`          varchar(64)   not null,
    `channel`               varchar(32)   not null,
    `sender_number`         varchar(32)   not null,
    `customer_number`       varchar(32)   not null,
    `content`               varchar(1600) null,
    `status`                int           not null,
    `failure_reason`        varchar(2048) null,
    `retry_count`           int           not null default 0,
    `twilio_message_sid`    varchar(64)   null,
    `created_date`          datetime(6)   not null,
    `sent_date`             datetime(6)   null,
    `last_modified_date`    datetime(6)   null,
    primary key (`id`),
    unique key `uk_call_notification_record_call_channel` (`phone_order_record_id`, `channel`),
    key `idx_call_notification_record_company_store_created` (`company_id`, `store_id`, `created_date`),
    key `idx_call_notification_record_customer_number` (`customer_number`)
);
