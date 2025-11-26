INSERT INTO permission (created_date, last_modified_date, name, is_system)
VALUES(NOW(3), NOW(3), 'CanViewDataDashboard', 1);

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'SuperAdministrator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'Administrator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'User';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'Operator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'ServiceProviderOperator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'TestOperator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'TestAdmin';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'TestServiceProviderOperator';

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r JOIN permission p ON p.name IN ('CanViewDataDashboard')
WHERE r.name = 'TestSuperAdministrator';