create table if not exists `pos_order_record_unread`
(
    `id` int primary key auto_increment,
    `record_id` int not null,
    `pos_store_user_id` int not null,
    `created_date` datetime(3) not null,
    `last_modified_by` int null,
    `last_modified_date` datetime(3) not null
    )charset=utf8mb4;

alter table phone_order_record add is_read tinyint(1) null;