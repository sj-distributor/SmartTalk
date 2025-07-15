INSERT INTO role (created_on, modified_on, uuid, name)
VALUES
    (now(3), now(3), uuid(), 'Operator'),,
    (now(3), now(3), uuid(), 'ServiceProviderAdministrator'),
    (now(3), now(3), uuid(), 'ServiceProviderOperator');

INSERT INTO permission (created_date, last_modified_date, name, is_system)
VALUES
    (NOW(3), NOW(3), 'CanViewAutoCall', 1),
    (NOW(3), NOW(3), 'CanViewKnowledge', 1),
    (NOW(3), NOW(3), 'CanViewPlaceOrder', 1),
    (NOW(3), NOW(3), 'CanViewBusinessManagement', 1);

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewAutoCall','CanViewKnowledge', 'CanViewPlaceOrder')
WHERE r.name = 'Operator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder','CanViewPlaceOrder')
WHERE r.name = 'ServiceProviderOperator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder','CanViewAccountManagement', 'CanCreateAccount','CanDeleteAccount','CanCopyAccount','CanUpdateAccount','CanViewPlaceOrder',
                                           'CanViewBusinessManagement')
WHERE r.name = 'ServiceProviderAdministrator';

ALTER TABLE `user_account` ADD COLUMN `account_level` VARCHAR(36) NOT NULL;