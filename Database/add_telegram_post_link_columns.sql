-- Привязка поста в канале к товару (для бронирования по первому сообщению)
-- Выполнить: mysql -u root -p bebochka < add_telegram_post_link_columns.sql

USE bebochka;

-- TelegramMessageId: ID сообщения в канале
SET @col1 = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'TelegramMessageId');
SET @sql1 = IF(@col1 = 0, 'ALTER TABLE Products ADD COLUMN TelegramMessageId INT NULL COMMENT ''ID поста в Telegram канале'' AFTER PublishedAt', 'SELECT ''TelegramMessageId exists''');
PREPARE stmt FROM @sql1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- TelegramChatId: ID чата канала (например -1001234567890)
SET @col2 = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'TelegramChatId');
SET @sql2 = IF(@col2 = 0, 'ALTER TABLE Products ADD COLUMN TelegramChatId VARCHAR(50) NULL COMMENT ''ID канала Telegram'' AFTER TelegramMessageId', 'SELECT ''TelegramChatId exists''');
PREPARE stmt FROM @sql2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Индекс для поиска товара по посту
SET @idx = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Products' AND INDEX_NAME = 'ix_products_telegram_post');
SET @sql3 = IF(@idx = 0, 'CREATE INDEX ix_products_telegram_post ON Products(TelegramChatId, TelegramMessageId)', 'SELECT ''Index exists''');
PREPARE stmt FROM @sql3; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT 'Telegram post link columns added.' AS message;

