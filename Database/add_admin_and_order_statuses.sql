-- Скрипт для добавления поддержки администраторов и статусов заказов
-- Выполнить: mysql -u root -p bebochka < add_admin_and_order_statuses.sql

USE bebochka;

-- 1. Добавляем поле IsAdmin в таблицу Users
-- Проверяем, существует ли колонка IsAdmin
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Users' 
    AND COLUMN_NAME = 'IsAdmin'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Users ADD COLUMN IsAdmin TINYINT(1) NOT NULL DEFAULT 0 COMMENT ''Флаг администратора (0 - пользователь, 1 - администратор)'' AFTER IsActive',
    'SELECT ''Column IsAdmin already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 2. Обновляем существующие статусы заказов для соответствия новой системе
-- Статусы: pending -> В сборке, confirmed -> Ожидает оплату, shipped -> В пути, completed -> Доставлен, cancelled -> Отменен

-- Создаем временную таблицу для маппинга старых статусов на новые
UPDATE Orders 
SET Status = CASE 
    WHEN Status = 'pending' THEN 'В сборке'
    WHEN Status = 'confirmed' THEN 'Ожидает оплату'
    WHEN Status = 'shipped' THEN 'В пути'
    WHEN Status = 'completed' THEN 'Доставлен'
    WHEN Status = 'cancelled' THEN 'Отменен'
    ELSE Status
END
WHERE Status IN ('pending', 'confirmed', 'shipped', 'completed', 'cancelled');

-- 3. Добавляем поле UserId в таблицу Orders для связи заказа с пользователем (если еще не существует)
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Orders' 
    AND COLUMN_NAME = 'UserId'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Orders ADD COLUMN UserId INT NULL COMMENT ''ID пользователя, создавшего заказ'' AFTER Id, ADD INDEX idx_orders_userid (UserId)',
    'SELECT ''Column UserId already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 4. Добавляем поле CancelledAt для отслеживания времени отмены заказа
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Orders' 
    AND COLUMN_NAME = 'CancelledAt'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Orders ADD COLUMN CancelledAt DATETIME NULL COMMENT ''Дата и время отмены заказа'' AFTER UpdatedAt',
    'SELECT ''Column CancelledAt already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 5. Добавляем поле CancellationReason для причины отмены
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Orders' 
    AND COLUMN_NAME = 'CancellationReason'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Orders ADD COLUMN CancellationReason VARCHAR(500) NULL COMMENT ''Причина отмены заказа'' AFTER CancelledAt',
    'SELECT ''Column CancellationReason already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 6. Создаем индекс для быстрого поиска заказов по статусу и пользователю
CREATE INDEX IF NOT EXISTS idx_orders_status_userid ON Orders(Status, UserId);

-- 7. Пример: Назначить пользователя с ID=1 администратором (раскомментируйте при необходимости)
-- UPDATE Users SET IsAdmin = 1 WHERE Id = 1;

SELECT 'Database schema updated successfully!' AS message;
SELECT 'Added columns: Users.IsAdmin, Orders.UserId, Orders.CancelledAt, Orders.CancellationReason' AS changes;
SELECT 'Updated order statuses to new format' AS status_update;

