create table if not exists service_provider
(
    `id` int primary key auto_increment,
    `name` varchar(36) not null,
    `created_date` datetime(3) not null
    ) charset=utf8mb4;

INSERT INTO service_provider (name, created_date)
VALUES 
    ('SmartTalk', now(3)),
    ('TestOmeNow', now(3));

alter table pos_company add column service_provider_id int null;

alter table pos_company rename to company;

alter table pos_company_store rename to company_store;

alter table pos_store_user rename to store_user;

alter table user_account add column service_provider_id int null;

alter table agent add column service_provider_id int null;

alter table role add column service_provider_id int null;

alter table role add column user_account_level int null;

INSERT INTO role (created_on, modified_on, uuid, name, user_account_level)
VALUES
    (now(3), now(3), uuid(), 'TestOperator', 3),
    (now(3), now(3), uuid(), 'TestAdmin', 1),
    (now(3), now(3), uuid(), 'TestServiceProviderOperator', 1);

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder','CanViewKnowledge')
WHERE r.name = 'TestOperator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder','CanUpdateAccount','CanViewAccountManagement','CanCreateAccount','CanDeleteAccount','CanCopyAccount','CanViewBusinessManagement')
WHERE r.name = 'TestAdmin';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder','CanUpdateAccount','CanViewAccountManagement','CanCreateAccount','CanDeleteAccount','CanCopyAccount','CanViewBusinessManagement')
WHERE r.name = 'TestServiceProviderOperator';