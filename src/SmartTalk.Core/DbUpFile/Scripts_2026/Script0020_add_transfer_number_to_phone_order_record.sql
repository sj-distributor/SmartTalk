alter table phone_order_record
    add column `transfer_call_number` varchar(256) null after `incoming_call_number`;