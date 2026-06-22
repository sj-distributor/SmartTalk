ALTER TABLE `crm_sales_auto_sync_run`
    ADD COLUMN `created_stores_json` LONGTEXT NULL AFTER `warnings_json`,
    ADD COLUMN `created_agents_json` LONGTEXT NULL AFTER `created_stores_json`,
    ADD COLUMN `created_assistants_json` LONGTEXT NULL AFTER `created_agents_json`,
    ADD COLUMN `transferred_assistants_json` LONGTEXT NULL AFTER `created_assistants_json`,
    ADD COLUMN `renamed_assistants_json` LONGTEXT NULL AFTER `transferred_assistants_json`,
    ADD COLUMN `deactivated_assistants_json` LONGTEXT NULL AFTER `renamed_assistants_json`;
