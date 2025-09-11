DELETE FROM role_permission
WHERE role_id = (SELECT id FROM role WHERE name = 'SuperAdministrator')
  AND permission_id IN (
    SELECT id FROM permission
    WHERE name IN ('CanViewAutoCall','CanViewKnowledge')
);

