-- Только таблица Announcements для уже существующей БД (например локальная bebochka).
-- Выполнить: mysql -u bebochka -p bebochka < migration_20250407_announcements_only.sql

USE bebochka;

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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
