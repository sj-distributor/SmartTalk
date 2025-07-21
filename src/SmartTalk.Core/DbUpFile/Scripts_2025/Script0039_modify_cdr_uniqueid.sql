ALTER TABLE `cdr` ADD COLUMN `uniqueid1` DECIMAL(20,6);

UPDATE `cdr`
SET `uniqueid1` = CAST(`uniqueid` AS DECIMAL(20,6))
WHERE `uniqueid1` IS NULL
LIMIT 1000;

ALTER TABLE `cdr` DROP COLUMN `uniqueid`;

ALTER TABLE `cdr` CHANGE COLUMN `uniqueid1` `uniqueid` DECIMAL(20,6);
