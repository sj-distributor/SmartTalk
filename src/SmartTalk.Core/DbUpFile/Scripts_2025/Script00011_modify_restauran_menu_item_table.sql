alter table `restaurant_menu_item` add column `product_id` bigint null;

alter table `restaurant_menu_item` drop column `menu_item_id`;