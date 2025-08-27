DELETE FROM role_permission
WHERE role_id = (SELECT id FROM role WHERE name = 'Operator')
  AND permission_id = (SELECT id FROM permission WHERE name = 'CanViewPlaceOrder');

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewPhoneOrder')
WHERE r.name = 'Operator';
