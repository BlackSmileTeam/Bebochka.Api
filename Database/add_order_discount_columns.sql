-- Скидки по заказам (фиксированная и по условию)
-- Выполнить: mysql -u root -p bebochka < add_order_discount_columns.sql

USE bebochka;

-- DiscountType: None, Fixed, ByCondition
SET @col = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Orders' AND COLUMN_NAME = 'DiscountType');
SET @sql = IF(@col = 0, 'ALTER TABLE Orders ADD COLUMN DiscountType VARCHAR(20) NOT NULL DEFAULT ''None'' AFTER Status', 'SELECT ''DiscountType exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Orders' AND COLUMN_NAME = 'FixedDiscountPercent');
SET @sql = IF(@col = 0, 'ALTER TABLE Orders ADD COLUMN FixedDiscountPercent INT NULL AFTER DiscountType', 'SELECT ''FixedDiscountPercent exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Orders' AND COLUMN_NAME = 'Condition1ItemPercent');
SET @sql = IF(@col = 0, 'ALTER TABLE Orders ADD COLUMN Condition1ItemPercent INT NULL AFTER FixedDiscountPercent', 'SELECT ''Condition1ItemPercent exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Orders' AND COLUMN_NAME = 'Condition3ItemsPercent');
SET @sql = IF(@col = 0, 'ALTER TABLE Orders ADD COLUMN Condition3ItemsPercent INT NULL AFTER Condition1ItemPercent', 'SELECT ''Condition3ItemsPercent exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Orders' AND COLUMN_NAME = 'Condition5PlusPercent');
SET @sql = IF(@col = 0, 'ALTER TABLE Orders ADD COLUMN Condition5PlusPercent INT NULL AFTER Condition3ItemsPercent', 'SELECT ''Condition5PlusPercent exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT 'Order discount columns added' AS message;
