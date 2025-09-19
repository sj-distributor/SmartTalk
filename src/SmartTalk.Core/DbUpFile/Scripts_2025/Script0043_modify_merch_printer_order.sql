alter table `merch_printer_order` modify `order_id` int not null;

alter table `merch_printer_log` modify `order_id` int not null;

alter table `merch_printer`  CHANGE COLUMN `agent_id` `store_id` int not NULL;

alter table `merch_printer_order`  CHANGE COLUMN `agent_id` `store_id` int not NULL;

alter table `merch_printer_log`  CHANGE COLUMN `agent_id` `store_id` int not NULL;