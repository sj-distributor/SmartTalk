alter table `crm_customer_contact_phone_map`
    drop index `idx_crm_customer_contact_phone_map_agent_phone`;

alter table `crm_customer_contact_phone_map`  drop column `is_active`;

create index `idx_crm_customer_contact_phone_map_agent_phone` on `crm_customer_contact_phone_map` (`agent_id`, `contact_phone_normalized`);
