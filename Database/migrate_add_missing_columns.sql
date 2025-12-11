-- Скрипт для добавления недостающих колонок в базу данных
-- Добавляет:
-- 1. PublishedAt в таблицу Products
-- 2. TelegramUserId в таблицу Users
-- Выполнить: mysql -u root -p bebochka < migrate_add_missing_columns.sql

USE bebochka;

-- ============================================
-- 1. Добавление PublishedAt в таблицу Products
-- ============================================
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Products' 
    AND COLUMN_NAME = 'PublishedAt'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Products ADD COLUMN PublishedAt DATETIME NULL COMMENT ''Дата и время публикации товара. Если NULL - товар опубликован сразу'' AFTER UpdatedAt',
    'SELECT ''Column Products.PublishedAt already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Добавляем индекс для быстрого поиска товаров по дате публикации
SET @index_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.STATISTICS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Products' 
    AND INDEX_NAME = 'idx_products_published_at'
);

SET @sql = IF(@index_exists = 0,
    'CREATE INDEX idx_products_published_at ON Products(PublishedAt)',
    'SELECT ''Index idx_products_published_at already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ============================================
-- 2. Добавление TelegramUserId в таблицу Users
-- ============================================
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Users' 
    AND COLUMN_NAME = 'TelegramUserId'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Users ADD COLUMN TelegramUserId BIGINT NULL COMMENT ''Telegram User ID для связи с ботом'' AFTER Id',
    'SELECT ''Column Users.TelegramUserId already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Добавляем индекс для TelegramUserId (если еще нет)
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
SELECT 'Database migration completed successfully!' AS message;
SELECT 'Added columns:' AS summary;
SELECT '  - Products.PublishedAt' AS column1;
SELECT '  - Users.TelegramUserId' AS column2;

-- Показываем информацию о добавленных колонках
SELECT 
    TABLE_NAME AS 'Table',
    COLUMN_NAME AS 'Column',
    DATA_TYPE AS 'Type',
    IS_NULLABLE AS 'Nullable',
    COLUMN_COMMENT AS 'Comment'
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'bebochka' 
AND ((TABLE_NAME = 'Products' AND COLUMN_NAME = 'PublishedAt')
     OR (TABLE_NAME = 'Users' AND COLUMN_NAME = 'TelegramUserId'))
ORDER BY TABLE_NAME, COLUMN_NAME;

