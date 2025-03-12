UPDATE `phone_order_record`
SET `created_date` = CONVERT_TZ(`created_date`, 'America/Los_Angeles', 'UTC');