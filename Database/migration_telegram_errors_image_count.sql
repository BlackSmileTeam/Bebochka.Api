-- TelegramErrors: создать таблицу (нижний регистр на Linux) и колонку ImageCount.
-- Идемпотентно: безопасно запускать повторно.

USE bebochka;

CREATE TABLE IF NOT EXISTS telegramerrors (
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
SET @table_name := (
    SELECT TABLE_NAME
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = @db
      AND LOWER(TABLE_NAME) = 'telegramerrors'
    LIMIT 1
);

SET @has_col := IF(
    @table_name IS NULL,
    1,
    (
        SELECT COUNT(*)
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = @db
          AND TABLE_NAME = @table_name
          AND COLUMN_NAME = 'ImageCount'
    )
);

SET @sql := IF(
    @table_name IS NULL OR @has_col > 0,
    'SELECT ''ImageCount already exists or table missing'' AS info',
    CONCAT(
        'ALTER TABLE `', @table_name, '` ADD COLUMN ImageCount INT NULL AFTER ProductInfo'
    )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
