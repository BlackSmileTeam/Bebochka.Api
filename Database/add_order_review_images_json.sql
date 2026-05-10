-- Фото в отзывах (JSON-массив путей к файлам).
-- Ошибка API: Unknown column 'ReviewImagesJson' — выполните этот скрипт на БД приложения.
--
-- Вручную:
--   mysql -u USER -p bebochka_db < add_order_review_images_json.sql
-- Или в клиенте MySQL: SOURCE /path/to/add_order_review_images_json.sql;

SET @db := DATABASE();
SET @exists := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db
    AND TABLE_NAME = 'ordercustomerreviews'
    AND COLUMN_NAME = 'ReviewImagesJson'
);
SET @sql := IF(
  @exists = 0,
  'ALTER TABLE `ordercustomerreviews` ADD COLUMN `ReviewImagesJson` TEXT NULL',
  'SELECT ''skip: ReviewImagesJson already present'' AS `migration`'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
