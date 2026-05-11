-- Отзыв без заказа: OrderId NULL + ручные поля имени/телефона + ReviewImagesJson (если нет).
-- ОБЯЗАТЕЛЬНО выполните на продакшен-БД после деплоя API, иначе GET /api/orders/reviews → 500
--   (Unknown column 'ManualCustomerName' / 'ReviewImagesJson').

SET @db := DATABASE();

-- Снять внешний ключ ordercustomerreviews -> orders (имя ограничения может отличаться)
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
PREPARE stmt1 FROM @drop;
EXECUTE stmt1;
DEALLOCATE PREPARE stmt1;

-- Колонка OrderId допускает NULL; уникальный индекс по OrderId в MySQL допускает несколько NULL
ALTER TABLE `ordercustomerreviews` MODIFY COLUMN `OrderId` INT NULL;

SET @hasManualName := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ordercustomerreviews' AND COLUMN_NAME = 'ManualCustomerName'
);
SET @sql2 := IF(@hasManualName = 0,
  'ALTER TABLE `ordercustomerreviews` ADD COLUMN `ManualCustomerName` VARCHAR(255) NULL, ADD COLUMN `ManualCustomerPhone` VARCHAR(100) NULL',
  'SELECT ''skip: ManualCustomerName exists'' AS `msg`');
PREPARE stmt2 FROM @sql2;
EXECUTE stmt2;
DEALLOCATE PREPARE stmt2;

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

-- Колонка ReviewImagesJson (фото отзыва), если ещё не добавлена
SET @hasReviewImages := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ordercustomerreviews' AND COLUMN_NAME = 'ReviewImagesJson'
);
SET @sql4 := IF(@hasReviewImages = 0,
  'ALTER TABLE `ordercustomerreviews` ADD COLUMN `ReviewImagesJson` TEXT NULL',
  'SELECT ''skip: ReviewImagesJson exists'' AS `msg`');
PREPARE stmt4 FROM @sql4;
EXECUTE stmt4;
DEALLOCATE PREPARE stmt4;
