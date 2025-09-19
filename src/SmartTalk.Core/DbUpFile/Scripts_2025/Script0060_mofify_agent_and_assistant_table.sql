alter table `agent` add column `name` varchar(255) null;
alter table `agent` add column `brief` varchar(512) null;
alter table `agent` add column `channel` int null;
alter table `agent` add column `is_receive_call` tinyint not null default 1;