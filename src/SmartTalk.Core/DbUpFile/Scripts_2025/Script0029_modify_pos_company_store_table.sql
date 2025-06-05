alter table `pos_company_store` add `pos_id` varchar(64) null;
alter table `pos_company_store` add `pos_name` varchar(64) null;
alter table `pos_company_store` add `is_link` tinyint(1) not null default 0;