alter table `phone_order_record` modify column `url` varchar(255) null;
alter table `phone_order_record` modify column `transcription_text` text null;
alter table `phone_order_record` add column `language` int not null default 0;