INSERT INTO company_setting (`company_id`, `setting_key`, `setting_value`)
SELECT `id`, 'is_complaint_analysis_enabled', '1'
FROM `company`
WHERE `is_complaint_analysis_enabled` = 1;

ALTER TABLE `company` DROP COLUMN `is_complaint_analysis_enabled`;