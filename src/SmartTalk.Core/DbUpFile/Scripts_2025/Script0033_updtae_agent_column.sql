alter table `agent` modify column `relate_id` int null;
alter table `agent` add column `domain_id` int null;
alter table `agent` add column `is_display` tinyint(1) not null default 1;
