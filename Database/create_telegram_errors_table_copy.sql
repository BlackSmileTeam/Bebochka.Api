-- SQL script for creating TelegramErrors table
-- Execute this script to create the errors table

USE bebochka;

-- Create TelegramErrors table
CREATE TABLE IF NOT EXISTS TelegramErrors (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ErrorDate DATETIME NOT NULL,
    Message VARCHAR(1000) NOT NULL,
    Details TEXT,
    ErrorType VARCHAR(100) NOT NULL,
    ProductInfo VARCHAR(500),
    ImageCount INT,
    ChannelId VARCHAR(100),
    INDEX idx_error_date (ErrorDate),
    INDEX idx_error_type (ErrorType)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Verify table creation
SHOW TABLES LIKE 'TelegramErrors';

-- Verify table structure
DESCRIBE TelegramErrors;
