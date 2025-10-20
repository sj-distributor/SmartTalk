alter table `agent` add column `voice` varchar(64) null;
alter table `agent` add column `wait_interval` int not null default 500;
alter table `agent` add column `transfer_call_number` varchar(128) null;
alter table `agent` add column `is_transfer_human` tinyint(1) not null default 0;
