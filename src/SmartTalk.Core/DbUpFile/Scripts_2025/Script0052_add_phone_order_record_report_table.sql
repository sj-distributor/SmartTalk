alter table `user_account` add column `language` varchar(36) null;

create table if not exists phnoe_order_record_report
(
    `id` int primary key auto_increment,
    `record_id` int not null,
    `language` int not null,
    `report` text not null,
    `created_date` datetime(3) not null
    ) charset=utf8mb4;

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanUpdateAccount')
WHERE r.name = 'Administrator';