-- Ссылка на профиль/чат клиента (tg://openmessage?user_id=... или https://t.me/username)
-- Выполнить: mysql -u root -p bebochka < add_order_customer_profile_link.sql

USE bebochka;

SET @col_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'bebochka'
      AND TABLE_NAME = 'Orders'
      AND COLUMN_NAME = 'CustomerProfileLink'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Orders ADD COLUMN CustomerProfileLink VARCHAR(500) NULL COMMENT ''Ссылка на профиль/чат (tg или t.me)'' AFTER CustomerName',
    'SELECT ''Column CustomerProfileLink already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT 'Orders.CustomerProfileLink added' AS message;
