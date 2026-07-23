create table if not exists `crm_customer_contact_phone_map`
(
    `id` bigint not null auto_increment primary key,
    `company_id` int not null,
    `agent_id` int not null,
    `assistant_id` int not null,
    `customer_id` varchar(64) not null,
    `customer_name` varchar(255) null,
    `contact_name` varchar(255) null,
    `contact_identity` varchar(255) null,
    `contact_language` varchar(64) null,
    `contact_phone_raw` varchar(64) null,
    `contact_phone_normalized` varchar(32) not null,
    `is_active` tinyint(1) not null default 1,
    `created_by` int null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) null
);

create index `idx_crm_customer_contact_phone_map_agent_phone` on `crm_customer_contact_phone_map` (`agent_id`, `contact_phone_normalized`, `is_active`);
create unique index `uq_crm_customer_contact_phone_map_customer_agent_phone` on `crm_customer_contact_phone_map` (`customer_id`, `agent_id`, `assistant_id`, `contact_phone_normalized`);
