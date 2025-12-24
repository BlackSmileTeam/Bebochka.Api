-- Скрипт для проверки статуса публикации товаров
-- Использование: mysql -u root -p bebochka < check_publication_status.sql

USE bebochka;

-- Проверка товаров с установленной датой публикации
SELECT 
    Id,
    Name,
    PublishedAt,
    DATE_FORMAT(PublishedAt, '%Y-%m-%d %H:%i:%s') AS PublishedAtFormatted,
    UTC_TIMESTAMP() AS CurrentUTCTime,
    DATE_FORMAT(UTC_TIMESTAMP(), '%Y-%m-%d %H:%i:%s') AS CurrentUTCTimeFormatted,
    TIMESTAMPDIFF(MINUTE, PublishedAt, UTC_TIMESTAMP()) AS MinutesSincePublication,
    CASE 
        WHEN PublishedAt IS NULL THEN 'Не установлено'
        WHEN PublishedAt > UTC_TIMESTAMP() THEN CONCAT('Будет опубликовано через ', TIMESTAMPDIFF(MINUTE, UTC_TIMESTAMP(), PublishedAt), ' мин.')
        WHEN PublishedAt <= UTC_TIMESTAMP() AND PublishedAt > DATE_SUB(UTC_TIMESTAMP(), INTERVAL 30 MINUTE) THEN 'Должно быть отправлено уведомление'
        ELSE 'Пропущено (более 30 минут назад)'
    END AS PublicationStatus
FROM Products
WHERE PublishedAt IS NOT NULL
ORDER BY PublishedAt DESC
LIMIT 20;

-- Проверка зарегистрированных Telegram пользователей
SELECT 
    Id,
    Username,
    TelegramUserId,
    IsActive,
    CreatedAt,
    DATE_FORMAT(CreatedAt, '%Y-%m-%d %H:%i:%s') AS CreatedAtFormatted
FROM Users
WHERE TelegramUserId IS NOT NULL
ORDER BY CreatedAt DESC;

