-- Исправление отзывов на продакшен-БД (идемпотентно):
--   GET  /api/orders/reviews       — Unknown column ManualCustomerName / ReviewImagesJson
--   POST /api/orders/reviews/admin — Column 'OrderId' cannot be null (ручной отзыв без заказа)
--
-- На сервере:
--   mysql -h 127.0.0.1 -u USER -p ИМЯ_БД < reviews_add_manual_columns_if_missing.sql
--
-- Полная логика совпадает с review_order_optional.sql (можно выполнить любой из двух один раз).

SET @db := DATABASE();

-- 1) Снять FK ordercustomerreviews -> orders (имя ограничения может отличаться)
SET @fk := (
  SELECT kcu.CONSTRAINT_NAME
  FROM information_schema.KEY_COLUMN_USAGE kcu
  WHERE kcu.TABLE_SCHEMA = @db
    AND kcu.TABLE_NAME = 'ordercustomerreviews'
    AND kcu.REFERENCED_TABLE_NAME = 'orders'
  LIMIT 1
);

SET @drop := IF(@fk IS NOT NULL,
  CONCAT('ALTER TABLE `ordercustomerreviews` DROP FOREIGN KEY `', @fk, '`'),
  'SELECT ''skip: no FK on ordercustomerreviews.OrderId'' AS `msg`');
PREPARE stmt0 FROM @drop;
EXECUTE stmt0;
DEALLOCATE PREPARE stmt0;

-- 2) OrderId допускает NULL (ручной отзыв без заказа)
ALTER TABLE `ordercustomerreviews` MODIFY COLUMN `OrderId` INT NULL;

-- 3) Ручные поля клиента
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

-- 4) Вернуть FK на orders (если ещё нет)
SET @fkExists := (
  SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ordercustomerreviews'
    AND REFERENCED_TABLE_NAME = 'orders'
);
SET @addFk := IF(@fkExists = 0,
  'ALTER TABLE `ordercustomerreviews` ADD CONSTRAINT `FK_ordercustomerreviews_orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`) ON DELETE CASCADE',
  'SELECT ''skip: FK orders already on ordercustomerreviews'' AS `msg`');
PREPARE stmt3 FROM @addFk;
EXECUTE stmt3;
DEALLOCATE PREPARE stmt3;

-- 5) JSON фото отзыва
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
