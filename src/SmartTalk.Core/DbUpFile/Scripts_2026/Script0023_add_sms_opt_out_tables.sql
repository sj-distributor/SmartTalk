create table `sms_sender_number`
(
    `id`           int          not null auto_increment,
    `company_id`   int          not null,
    `phone_number` varchar(32)  not null,
    `created_date` datetime(6)  not null,
    primary key (`id`),
    unique key `uk_sms_sender_number_phone_number` (`phone_number`)
);

create table `sms_customer_opt_out`
(
    `id`                    int          not null auto_increment,
    `company_id`            int          not null,
    `customer_phone_number` varchar(32)  not null,
    `opted_out_at`          datetime(6)  not null,
    primary key (`id`),
    unique key `uk_sms_customer_opt_out_company_customer` (`company_id`, `customer_phone_number`),
    key `idx_sms_customer_opt_out_customer_phone_number` (`customer_phone_number`)
);
