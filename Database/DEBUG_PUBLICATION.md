# Отладка проблемы с публикацией товаров

## Проблема

Товары, установленные на публикацию в 10:42, не отправляют уведомления.

## Возможные причины

1. **Разница часовых поясов:**
   - Сервер использует UTC
   - Пользователь устанавливает время в московском времени (UTC+3)
   - Если установлено 10:42 МСК, то в UTC это 07:42
   - Проверка происходит каждый минуту, но товары проверяются только если они опубликованы в последние 15 минут

2. **Проблема с конвертацией времени:**
   - Frontend отправляет время в ISO формате (UTC)
   - Но возможно время конвертируется неправильно

## Проверка

### 1. Проверить время в базе данных

```sql
USE bebochka;

SELECT 
    Id,
    Name,
    PublishedAt,
    DATE_FORMAT(PublishedAt, '%Y-%m-%d %H:%i:%s') AS PublishedAtFormatted,
    NOW() AS ServerTime,
    DATE_FORMAT(NOW(), '%Y-%m-%d %H:%i:%s') AS ServerTimeFormatted,
    TIMESTAMPDIFF(MINUTE, PublishedAt, NOW()) AS MinutesAgo
FROM Products
WHERE PublishedAt IS NOT NULL
ORDER BY PublishedAt DESC
LIMIT 10;
```

### 2. Проверить логи сервиса

В логах должны быть записи:
- `Checking for publications at UTC time: ...`
- `Found X products ready for publication`
- `Publication notification sent to X users`

### 3. Проверить зарегистрированных пользователей Telegram

```sql
SELECT 
    Id,
    Username,
    TelegramUserId,
    IsActive,
    CreatedAt
FROM Users
WHERE TelegramUserId IS NOT NULL AND IsActive = 1;
```

## Решение

1. Убедитесь, что время на сервере правильное (UTC)
2. Проверьте, что PublishedAt сохраняется в UTC
3. Проверьте логи для понимания, что происходит

## Временная зона

Московское время = UTC + 3 часа

Если вы установили публикацию на 10:42 (МСК), в базе данных должно быть сохранено 07:42 UTC.

## Проверка логики

Сервис проверяет товары каждую минуту и ищет товары, которые:
- `PublishedAt <= NOW()` (уже наступило время публикации)
- `PublishedAt > NOW() - 15 минут` (опубликованы в последние 15 минут)

Если товар был опубликован более 15 минут назад, он будет пропущен.

