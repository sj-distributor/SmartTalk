create table if not exists role_permission_user
(
    id int auto_increment primary key,
    created_date datetime(3) not null,
    modified_date datetime(3) not null,
    role_id int not null,
    permission_id int not null,
    user_ids text not null,
    constraint idx_role_id_unit_id
        unique (role_id, permission_id)
)charset=utf8mb4;

insert into role (created_on, modified_on, uuid, name)
VALUES (now(3), now(3), uuid(), 'SuperAdministrator'),
       (now(3), now(3), uuid(),'Administrator'),
       (now(3), now(3), uuid(),'User');

INSERT INTO permission (created_date, last_modified_date, name, is_system)
VALUES
    (NOW(3), NOW(3), 'CanViewPhoneOrder', 1),
    (NOW(3), NOW(3), 'CanViewAccountManagement', 1),
    (NOW(3), NOW(3), 'CanCreateAccount', 1),
    (NOW(3), NOW(3), 'CanDeleteAccount', 1),
    (NOW(3), NOW(3), 'CanCopyAccount', 1),
    (NOW(3), NOW(3), 'CanUpdateAccount', 1);

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder','CanViewAccountManagement', 'CanCreateAccount','CanDeleteAccount','CanCopyAccount','CanUpdateAccount')
WHERE r.name = 'SuperAdministrator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder','CanViewAccountManagement','CanCopyAccount','')
WHERE r.name = 'Administrator';

