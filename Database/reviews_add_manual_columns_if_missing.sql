-- Быстрое исправление 500: Unknown column 'ManualCustomerName' на GET /api/orders/reviews
-- Только добавление колонок (идемпотентно). Не трогает FK и OrderId.
--
-- На сервере (подставьте пользователя, хост и имя БД):
--   mysql -h 127.0.0.1 -u USER -p ИМЯ_БД < reviews_add_manual_columns_if_missing.sql
--
-- Полная миграция (nullable OrderId, FK): см. review_order_optional.sql

SET @db := DATABASE();

SET @hasManualName := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ordercustomerreviews' AND COLUMN_NAME = 'ManualCustomerName'
);
SET @sql1 := IF(@hasManualName = 0,
  'ALTER TABLE `ordercustomerreviews` ADD COLUMN `ManualCustomerName` VARCHAR(255) NULL, ADD COLUMN `ManualCustomerPhone` VARCHAR(100) NULL',
  'SELECT ''skip: ManualCustomerName already exists'' AS `msg`');
PREPARE stmt1 FROM @sql1;
EXECUTE stmt1;
DEALLOCATE PREPARE stmt1;

SET @hasReviewImages := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ordercustomerreviews' AND COLUMN_NAME = 'ReviewImagesJson'
);
SET @sql2 := IF(@hasReviewImages = 0,
  'ALTER TABLE `ordercustomerreviews` ADD COLUMN `ReviewImagesJson` TEXT NULL',
  'SELECT ''skip: ReviewImagesJson already exists'' AS `msg`');
PREPARE stmt2 FROM @sql2;
EXECUTE stmt2;
DEALLOCATE PREPARE stmt2;
