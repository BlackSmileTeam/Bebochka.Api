-- SQL скрипт для создания таблицы Brands
-- Выполните этот скрипт для создания таблицы брендов

USE bebochka;

-- Создание таблицы Brands
CREATE TABLE IF NOT EXISTS Brands (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL UNIQUE,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Проверка создания таблицы
SHOW TABLES LIKE 'Brands';

-- Проверка структуры таблицы
DESCRIBE Brands;

