alter table `phone_order_conversation` add column `intent` int not null default 0;
alter table `phone_order_conversation` add column `extract_food_item` text null;