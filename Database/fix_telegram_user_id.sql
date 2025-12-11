-- Скрипт для добавления поля TelegramUserId в таблицу Users (если его еще нет)
-- Выполнить: mysql -u root -p bebochka < fix_telegram_user_id.sql

USE bebochka;

-- Добавляем поле TelegramUserId, если его еще нет
-- Проверяем существование колонки перед добавлением
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Users' 
    AND COLUMN_NAME = 'TelegramUserId'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Users ADD COLUMN TelegramUserId BIGINT NULL COMMENT ''Telegram User ID для связи с ботом'' AFTER Id',
    'SELECT ''Column TelegramUserId already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Добавляем индекс
SET @index_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.STATISTICS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Users' 
    AND INDEX_NAME = 'idx_users_telegram_userid'
);

SET @sql = IF(@index_exists = 0,
    'CREATE INDEX idx_users_telegram_userid ON Users(TelegramUserId)',
    'SELECT ''Index idx_users_telegram_userid already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Проверяем результат
SELECT 'Database schema updated successfully!' AS message;
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'bebochka' 
AND TABLE_NAME = 'Users' 
AND COLUMN_NAME = 'TelegramUserId';

