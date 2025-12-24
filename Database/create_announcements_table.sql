-- SQL скрипт для создания таблицы Announcements
-- Выполните этот скрипт для создания таблицы анонсов

USE bebochka;

-- Создание таблицы Announcements
CREATE TABLE IF NOT EXISTS Announcements (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Message TEXT NOT NULL,
    ScheduledAt DATETIME NOT NULL,
    ProductIds JSON NOT NULL DEFAULT '[]',
    CollageImages JSON NOT NULL DEFAULT '[]',
    IsSent BOOLEAN NOT NULL DEFAULT FALSE,
    SentAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    SentCount INT NOT NULL DEFAULT 0,
    INDEX idx_scheduled_at (ScheduledAt),
    INDEX idx_is_sent (IsSent)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Проверка создания таблицы
SHOW TABLES LIKE 'Announcements';

-- Проверка структуры таблицы
DESCRIBE Announcements;

