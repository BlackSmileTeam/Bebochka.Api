-- SQL скрипт для добавления таблицы Users в базу данных
-- Выполните этот скрипт для обновления структуры БД

USE bebochka;

-- Создание таблицы Users
CREATE TABLE IF NOT EXISTS Users (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Email VARCHAR(100) NULL,
    FullName VARCHAR(100) NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastLoginAt DATETIME NULL,
    INDEX idx_username (Username),
    INDEX idx_is_active (IsActive)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Проверка создания таблицы
SHOW TABLES LIKE 'Users';

-- Проверка структуры таблицы
DESCRIBE Users;

-- Примечание: Администратор создается автоматически при первом запуске приложения
-- Username: admin
-- Password: Admin123!
-- ВАЖНО: Смените пароль после первого входа!

