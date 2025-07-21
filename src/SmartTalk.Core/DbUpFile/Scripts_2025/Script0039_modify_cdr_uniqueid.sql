SET @col_exists := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cdr'
    AND COLUMN_NAME = 'uniqueid1'
);

SET @sql := IF(@col_exists = 0,
  'ALTER TABLE `cdr` ADD COLUMN `uniqueid1` DECIMAL(20,6);',
  'SELECT "uniqueid1 is existed";'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

UPDATE `cdr`
SET `uniqueid1` = CAST(`uniqueid` AS DECIMAL(20,6))
WHERE `uniqueid1` IS NULL
    LIMIT 1000;

SET @col_exists := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cdr'
    AND COLUMN_NAME = 'uniqueid'
);

SET @sql := IF(@col_exists = 1,
  'ALTER TABLE `cdr` DROP COLUMN `uniqueid`;',
  'SELECT "uniqueid no existed";'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (
  SELECT COUNT(*)
  FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cdr'
    AND COLUMN_NAME = 'uniqueid1'
);

SET @sql := IF(@col_exists = 1,
  'ALTER TABLE `cdr` CHANGE COLUMN `uniqueid1` `uniqueid` DECIMAL(20,6);',
  'SELECT "uniqueid1 no existed";'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
