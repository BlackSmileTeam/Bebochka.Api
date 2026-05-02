-- Столбец CreatedAt для OrderItems (модель EF и запросы с сортировкой/фильтром по нему).
-- Без него создание заказа падает: Unknown column 'CreatedAt' in 'field list'.
-- Выполнить: mysql -u root -p bebochka < add_order_items_created_at.sql

USE bebochka;

SET @col_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'OrderItems'
      AND COLUMN_NAME = 'CreatedAt'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE OrderItems ADD COLUMN CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT ''Время добавления позиции''',
    'SELECT ''Column OrderItems.CreatedAt already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT 'OrderItems.CreatedAt migration done' AS message;
