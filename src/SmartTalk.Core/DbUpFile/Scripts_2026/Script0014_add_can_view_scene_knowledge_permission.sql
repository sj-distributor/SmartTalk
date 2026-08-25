INSERT INTO permission (created_date, last_modified_date, name, is_system)
SELECT NOW(3), NOW(3), 'CanViewSceneKnowledge', 1
WHERE NOT EXISTS (
    SELECT 1 FROM permission WHERE name = 'CanViewSceneKnowledge'
);

INSERT INTO role_permission (created_date, last_modified_date, role_id, permission_id)
SELECT NOW(3), NOW(3), r.id, p.id
FROM role r
JOIN permission p ON p.name = 'CanViewSceneKnowledge'
WHERE r.user_account_level = 1
  AND NOT EXISTS (
      SELECT 1
      FROM role_permission rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );

INSERT INTO permission_rating_level (permission_id, permission_level)
SELECT p.id, 0
FROM permission p
WHERE p.name = 'CanViewSceneKnowledge'
  AND NOT EXISTS (
      SELECT 1
      FROM permission_rating_level prl
      WHERE prl.permission_id = p.id AND prl.permission_level = 0
  );
