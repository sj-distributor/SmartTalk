alter table `pos_order` add column `remarks` varchar(512) null;
alter table `store_customer` change column `notes` `remarks` varchar(512) null;

