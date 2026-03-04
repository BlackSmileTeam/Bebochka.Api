-- Отметка «добавлен в посылку» для позиции заказа
-- Выполнить: mysql -u root -p bebochka < add_order_item_added_to_parcel.sql

USE bebochka;

SET @col_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'bebochka'
      AND TABLE_NAME = 'OrderItems'
      AND COLUMN_NAME = 'AddedToParcel'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE OrderItems ADD COLUMN AddedToParcel TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''Добавлен в посылку''',
    'SELECT ''Column AddedToParcel already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT 'OrderItems.AddedToParcel added' AS message;
