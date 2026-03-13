alter table `merch_printer_order`
    add column `phone_order_Id` int null;

alter table `merch_printer_order`
    change column `order_id` `order_id` int null;