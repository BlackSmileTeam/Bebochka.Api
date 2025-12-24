# Инструкция по связыванию Telegram User ID

## Проблема

Если вы видите в логах:
```
warn: No Telegram users found for broadcast
Publication notification sent to 0 users
```

Это означает, что ваш Telegram User ID не привязан к пользователю в базе данных.

## Решение

### Шаг 1: Получите ваш Telegram User ID

Выберите один из способов:

**Способ 1: Через бота @userinfobot** (самый простой)
1. Откройте Telegram
2. Найдите бота [@userinfobot](https://t.me/userinfobot)
3. Отправьте команду `/start`
4. Бот покажет ваш User ID (например: `123456789`)

**Способ 2: Через бота @getmyid_bot**
1. Откройте Telegram
2. Найдите бота [@getmyid_bot](https://t.me/getmyid_bot)
3. Отправьте любое сообщение
4. Бот ответит с вашим User ID

### Шаг 2: Свяжите Telegram User ID с вашим пользователем

**Вариант A: Через SQL (рекомендуется)**

Подключитесь к базе данных и выполните:

```sql
USE bebochka;

-- Замените 123456789 на ваш реальный Telegram User ID
-- Замените 'admin' на ваше имя пользователя
UPDATE Users SET TelegramUserId = 123456789 WHERE Username = 'admin';
```

**Пример:**
```sql
UPDATE Users SET TelegramUserId = 987654321 WHERE Username = 'admin';
```

**Проверка:**
```sql
SELECT Id, Username, TelegramUserId, IsActive FROM Users;
```

Убедитесь, что в колонке `TelegramUserId` указан ваш ID.

**Вариант B: Через API**

Если у вас есть доступ к API:

```bash
# Получите токен авторизации через /api/auth/login
# Затем выполните:

curl -X PUT "http://your-server/api/users/YOUR_USER_ID/telegram" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"telegramUserId": YOUR_TELEGRAM_USER_ID}'
```

### Шаг 3: Перезапустите приложение

После обновления базы данных перезапустите приложение, чтобы изменения вступили в силу.

### Шаг 4: Проверка

После перезапуска проверьте логи. При следующей публикации товара вы должны увидеть:

```
info: Found 1 products ready for publication
info: Publication notification sent to 1 users
```

И уведомление должно прийти в ваш Telegram.

## Важные замечания

1. **Telegram User ID** - это число (например: `123456789`), а не username
2. Убедитесь, что пользователь активен (`IsActive = true`)
3. Один Telegram User ID может быть привязан только к одному пользователю
4. После обновления базы данных обязательно перезапустите приложение

## Troubleshooting

**Если уведомления все еще не приходят:**

1. Проверьте, что токен Telegram бота правильно настроен в переменных окружения
2. Убедитесь, что пользователь активен: `SELECT * FROM Users WHERE Username = 'admin';`
3. Проверьте логи приложения на наличие ошибок отправки сообщений
4. Убедитесь, что вы написали боту хотя бы одно сообщение (это необходимо для инициализации чата)

