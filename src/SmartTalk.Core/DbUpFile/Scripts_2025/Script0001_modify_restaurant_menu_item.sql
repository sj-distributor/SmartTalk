alter table `restaurant_menu_item`
    add column `product_id` bigint null;

alter table `restaurant_menu_item`
    add column `order_item_modifiers` text null;