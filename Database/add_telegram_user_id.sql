-- Скрипт для добавления поля TelegramUserId в таблицу Users
-- Это позволит связать Telegram пользователей с пользователями в БД
-- Выполнить: mysql -u root -p bebochka < add_telegram_user_id.sql

USE bebochka;

-- Добавляем поле TelegramUserId в таблицу Users
SET @col_exists = (
    SELECT COUNT(*) 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'bebochka' 
    AND TABLE_NAME = 'Users' 
    AND COLUMN_NAME = 'TelegramUserId'
);

SET @sql = IF(@col_exists = 0,
    'ALTER TABLE Users ADD COLUMN TelegramUserId BIGINT NULL COMMENT ''Telegram User ID для связи с ботом'' AFTER Id, ADD INDEX idx_users_telegram_userid (TelegramUserId)',
    'SELECT ''Column TelegramUserId already exists'' AS message'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Пример: Связать существующего пользователя с Telegram User ID
-- UPDATE Users SET TelegramUserId = YOUR_TELEGRAM_USER_ID WHERE Id = USER_ID_IN_DB;

SELECT 'Database schema updated successfully!' AS message;
SELECT 'Added column: Users.TelegramUserId' AS changes;
SELECT 'Use UPDATE Users SET TelegramUserId = YOUR_TELEGRAM_USER_ID WHERE Id = USER_ID_IN_DB to link users' AS instruction;

