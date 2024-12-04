INSERT INTO role_user (created_on, modified_on, uuid, role_id, user_id)
SELECT 
    NOW(), 
    NOW(), 
    UUID(), 
    (SELECT r.id FROM role r WHERE r.Name = 'User') AS role_id, 
    u.id 
FROM
    user_account u
WHERE
    NOT EXISTS (SELECT 1 FROM role_user r WHERE r.user_id = u.id)
    AND u.created_on < '2024-11-30';