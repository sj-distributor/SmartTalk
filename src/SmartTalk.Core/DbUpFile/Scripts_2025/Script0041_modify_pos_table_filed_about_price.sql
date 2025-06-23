alter table `poe_order` modify column `tax` decimal(10,2) not null;
alter table `poe_order` modify column `total` decimal(10,2) not null;
alter table `poe_order` modify column `sub_total` decimal(10,2) not null;
alter table `pos_product` modify column `price` decimal(10,2) not null;