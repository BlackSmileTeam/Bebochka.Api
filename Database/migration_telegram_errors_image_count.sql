-- TelegramErrors: создать таблицу, если её нет; добавить ImageCount, если колонки нет.
-- Идемпотентно: безопасно запускать повторно.

USE bebochka;

CREATE TABLE IF NOT EXISTS TelegramErrors (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ErrorDate DATETIME NOT NULL,
    Message VARCHAR(1000) NOT NULL,
    Details TEXT,
    ErrorType VARCHAR(100) NOT NULL,
    ProductInfo VARCHAR(500),
    ImageCount INT NULL,
    ChannelId VARCHAR(100),
    INDEX idx_error_date (ErrorDate),
    INDEX idx_error_type (ErrorType)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET @db := DATABASE();
SET @has_col := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @db
      AND TABLE_NAME = 'TelegramErrors'
      AND COLUMN_NAME = 'ImageCount'
);

SET @sql := IF(
    @has_col = 0,
    'ALTER TABLE TelegramErrors ADD COLUMN ImageCount INT NULL AFTER ProductInfo',
    'SELECT ''ImageCount already exists'' AS info'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
