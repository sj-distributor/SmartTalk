INSERT INTO role (created_on, modified_on, uuid, name, user_account_level)
VALUES
    (now(3), now(3), uuid(), 'TestSuperAdministrator', 1);

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder','CanUpdateAccount','CanViewAccountManagement','CanCreateAccount','CanDeleteAccount','CanCopyAccount','CanViewBusinessManagement')
WHERE r.name = 'TestSuperAdministrator';