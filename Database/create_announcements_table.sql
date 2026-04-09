-- SQL скрипт для создания таблицы Announcements
-- Выполните этот скрипт для создания таблицы анонсов

USE bebochka;

-- Создание таблицы Announcements (имена колонок совпадают с маппингом EF / Pomelo)
CREATE TABLE IF NOT EXISTS Announcements (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Message TEXT NOT NULL,
    ScheduledAt DATETIME NOT NULL,
    ProductIds TEXT NOT NULL DEFAULT ('[]'),
    CollageImages TEXT NOT NULL DEFAULT ('[]'),
    IsSent TINYINT(1) NOT NULL DEFAULT 0,
    SentAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP()),
    SentCount INT NOT NULL DEFAULT 0,
    INDEX idx_scheduled_at (ScheduledAt),
    INDEX idx_is_sent (IsSent)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Проверка создания таблицы
SHOW TABLES LIKE 'Announcements';

-- Проверка структуры таблицы
DESCRIBE Announcements;

