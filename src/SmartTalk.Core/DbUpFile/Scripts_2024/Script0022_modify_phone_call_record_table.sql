RENAME TABLE `phone_order_record` TO `phone_call_record`;
RENAME TABLE `phone_order_order_item` TO `phone_call_order_item`;
RENAME TABLE `phone_order_conversation` TO `phone_call_conversation`;
       
alter table `phone_call_record` add column `call_status` int null;
alter table `phone_call_record` add column `agent` int not null default 0;

ALTER TABLE `phone_call_record` MODIFY `restaurant` int null;
ALTER TABLE `phone_call_record` MODIFY `language` int null;