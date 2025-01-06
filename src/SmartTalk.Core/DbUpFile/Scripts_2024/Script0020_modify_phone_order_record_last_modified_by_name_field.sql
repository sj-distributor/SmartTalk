UPDATE phone_order_record
SET last_modified_by_name = (
    SELECT ua.username
    FROM user_account ua
    WHERE phone_order_record.last_modified_by = ua.id
);