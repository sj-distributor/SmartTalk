INSERT INTO permission (created_date, last_modified_date, name, is_system)
VALUES
    (NOW(3), NOW(3), 'CanViewDynamicConfig', 1);

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r
JOIN permission p ON p.name = 'CanViewDynamicConfig'
WHERE r.name LIKE '%admin%';

