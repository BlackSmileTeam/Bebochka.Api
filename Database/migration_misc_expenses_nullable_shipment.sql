-- Мелкие расходы без привязки к поступлению (IncomingShipmentId = NULL)
-- mysql -u ... -p bebochka < migration_misc_expenses_nullable_shipment.sql

USE bebochka;

SET @tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'incomingshipmentexpenses'
  LIMIT 1
);

SET @ship_tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'incomingshipments'
  LIMIT 1
);

SET @nullable := (
  SELECT IS_NULLABLE
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = @tbl
    AND COLUMN_NAME = 'IncomingShipmentId'
  LIMIT 1
);

SET @fk_name := (
  SELECT CONSTRAINT_NAME
  FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE()
    AND TABLE_NAME = @tbl
    AND CONSTRAINT_TYPE = 'FOREIGN KEY'
  LIMIT 1
);

SET @sql_drop_fk := IF(
  @tbl IS NOT NULL AND @nullable = 'NO' AND @fk_name IS NOT NULL,
  CONCAT('ALTER TABLE `', @tbl, '` DROP FOREIGN KEY `', @fk_name, '`;'),
  'SELECT ''FK drop skipped'';'
);
PREPARE stmt_drop_fk FROM @sql_drop_fk;
EXECUTE stmt_drop_fk;
DEALLOCATE PREPARE stmt_drop_fk;

SET @sql_nullable := IF(
  @tbl IS NOT NULL AND @nullable = 'NO',
  CONCAT('ALTER TABLE `', @tbl, '` MODIFY COLUMN IncomingShipmentId INT NULL;'),
  'SELECT ''Column already nullable or table missing'';'
);
PREPARE stmt_nullable FROM @sql_nullable;
EXECUTE stmt_nullable;
DEALLOCATE PREPARE stmt_nullable;

SET @fk_exists := (
  SELECT COUNT(*)
  FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE()
    AND TABLE_NAME = @tbl
    AND CONSTRAINT_TYPE = 'FOREIGN KEY'
);

SET @sql_add_fk := IF(
  @tbl IS NOT NULL AND @ship_tbl IS NOT NULL AND @fk_exists = 0,
  CONCAT(
    'ALTER TABLE `', @tbl, '` ADD CONSTRAINT FK_IncomingShipmentExpenses_IncomingShipments ',
    'FOREIGN KEY (IncomingShipmentId) REFERENCES `', @ship_tbl, '` (Id) ON DELETE SET NULL;'
  ),
  'SELECT ''FK add skipped'';'
);
PREPARE stmt_add_fk FROM @sql_add_fk;
EXECUTE stmt_add_fk;
DEALLOCATE PREPARE stmt_add_fk;

SELECT 'misc expenses: IncomingShipmentId nullable' AS message;
