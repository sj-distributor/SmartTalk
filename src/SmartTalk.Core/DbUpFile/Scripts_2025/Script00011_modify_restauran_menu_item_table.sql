alter table `phone_order_order_item` add column `product_id` bigint null;

alter table `phone_order_order_item` drop column `menu_item_id`;