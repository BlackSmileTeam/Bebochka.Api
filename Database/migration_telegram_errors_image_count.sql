-- Добавляет колонку ImageCount в TelegramErrors (старые БД без неё ломают бэкап и EF).
-- Идемпотентно: безопасно запускать повторно.

USE bebochka;

SET @db := DATABASE();
SET @has_col := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @db
      AND TABLE_NAME = 'TelegramErrors'
      AND COLUMN_NAME = 'ImageCount'
);

SET @sql := IF(
    @has_col = 0,
    'ALTER TABLE TelegramErrors ADD COLUMN ImageCount INT NULL AFTER ProductInfo',
    'SELECT ''ImageCount already exists'' AS info'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
