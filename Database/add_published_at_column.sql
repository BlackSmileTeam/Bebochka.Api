-- Скрипт для добавления поля PublishedAt в таблицу Products
-- Выполнить: mysql -u root -p bebochka < add_published_at_column.sql

USE bebochka;

-- Добавляем поле PublishedAt, если его еще нет
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Products' 
    AND COLUMN_NAME = 'PublishedAt'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Products ADD COLUMN PublishedAt DATETIME NULL COMMENT ''Дата и время публикации товара. Если NULL - товар опубликован сразу'' AFTER UpdatedAt',
    'SELECT ''Column PublishedAt already exists'' AS message'
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

-- Проверяем результат
SELECT 'Database schema updated successfully!' AS message;
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'bebochka' 
AND TABLE_NAME = 'Products' 
AND COLUMN_NAME = 'PublishedAt';

