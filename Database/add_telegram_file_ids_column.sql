-- Добавление колонки TelegramFileIds в таблицу Products для кэша file_id Telegram
-- (предзагрузка фото в Telegram для быстрой публикации по расписанию)
-- Выполнить: mysql -u root -p bebochka < add_telegram_file_ids_column.sql

USE bebochka;

SET @col_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'bebochka'
    AND TABLE_NAME = 'Products'
    AND COLUMN_NAME = 'TelegramFileIds'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Products ADD COLUMN TelegramFileIds TEXT NULL COMMENT ''JSON array of Telegram file_id for pre-cached images'' AFTER Images',
    'SELECT ''Column TelegramFileIds already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT 'TelegramFileIds column added or already exists.' AS message;
