-- Удаляет дубликат TelegramErrors (PascalCase), оставляет telegramerrors (как в EF/bootstrap).
-- Переносит строки из PascalCase-таблицы, если она отдельная. Идемпотентно.

USE bebochka;

SET @db := DATABASE();

SET @pascal_table := (
    SELECT TABLE_NAME
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = @db
      AND TABLE_NAME = 'TelegramErrors'
    LIMIT 1
);

SET @lower_table := (
    SELECT TABLE_NAME
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = @db
      AND TABLE_NAME = 'telegramerrors'
    LIMIT 1
);

-- Каноническая таблица (нижний регистр)
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

SET @lower_table := COALESCE(@lower_table, 'telegramerrors');

SET @has_image_count := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @db
      AND TABLE_NAME = @lower_table
      AND COLUMN_NAME = 'ImageCount'
);

SET @sql_add_col := IF(
    @has_image_count > 0,
    'SELECT ''ImageCount ok'' AS info',
    CONCAT('ALTER TABLE `', @lower_table, '` ADD COLUMN ImageCount INT NULL AFTER ProductInfo')
);

PREPARE stmt_col FROM @sql_add_col;
EXECUTE stmt_col;
DEALLOCATE PREPARE stmt_col;

-- Две разные таблицы: перенос данных и удаление PascalCase
SET @sql_merge := IF(
    @pascal_table IS NULL OR @lower_table IS NULL OR @pascal_table = @lower_table,
    'SELECT ''No duplicate TelegramErrors table'' AS info',
    CONCAT(
        'INSERT INTO `', @lower_table, '` (Id, ErrorDate, Message, Details, ErrorType, ProductInfo, ImageCount, ChannelId) ',
        'SELECT p.Id, p.ErrorDate, p.Message, p.Details, p.ErrorType, p.ProductInfo, p.ImageCount, p.ChannelId ',
        'FROM `', @pascal_table, '` p ',
        'WHERE NOT EXISTS (SELECT 1 FROM `', @lower_table, '` t WHERE t.Id = p.Id)'
    )
);

PREPARE stmt_merge FROM @sql_merge;
EXECUTE stmt_merge;
DEALLOCATE PREPARE stmt_merge;

SET @sql_drop := IF(
    @pascal_table IS NULL OR @pascal_table = @lower_table,
    'SELECT ''Nothing to drop'' AS info',
    CONCAT('DROP TABLE `', @pascal_table, '`')
);

PREPARE stmt_drop FROM @sql_drop;
EXECUTE stmt_drop;
DEALLOCATE PREPARE stmt_drop;
