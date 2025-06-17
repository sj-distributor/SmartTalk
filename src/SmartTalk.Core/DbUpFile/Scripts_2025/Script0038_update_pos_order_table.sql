alter table `pos_order` add column `modified_items` text null;
alter table `pos_order` add column `is_push` tinyint(1) not null default 0;
