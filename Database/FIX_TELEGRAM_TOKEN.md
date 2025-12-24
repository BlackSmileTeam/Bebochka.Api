# Исправление проблемы с токеном Telegram бота

## Проблема

В логах видно ошибку:
```
POST https://api.telegram.org/bot/sendMessage
Failed to send message to chat 380172721. Status: NotFound
```

URL формируется без токена бота. Должно быть: `https://api.telegram.org/bot{TOKEN}/sendMessage`

## Решение

### Шаг 1: Проверьте переменную окружения в Docker контейнере

Подключитесь к серверу и проверьте, установлена ли переменная:

```bash
# Подключитесь к серверу
ssh root@89.104.67.36

# Проверьте переменные окружения контейнера
docker exec <container_id> env | grep TELEGRAM

# Или если знаете имя контейнера
docker exec bebochka-api env | grep TELEGRAM
```

### Шаг 2: Установите токен Telegram бота

**Вариант A: Через docker-compose.yml (рекомендуется)**

Если используете docker-compose, добавьте/проверьте переменную в `docker-compose.yml`:

```yaml
environment:
  - TelegramBot__Token=${TELEGRAM_BOT_TOKEN}
```

Затем создайте файл `.env` в том же каталоге:

```bash
# .env
TELEGRAM_BOT_TOKEN=ваш_токен_от_BotFather
```

**Вариант B: Через переменную окружения при запуске контейнера**

```bash
docker run -e TelegramBot__Token=ваш_токен_от_BotFather ... другие_параметры
```

**Вариант C: Экспортируйте переменную перед запуском**

```bash
export TELEGRAM_BOT_TOKEN=ваш_токен_от_BotFather
docker-compose up -d
```

### Шаг 3: Получите токен от BotFather

1. Найдите бота [@BotFather](https://t.me/BotFather) в Telegram
2. Отправьте команду `/newbot` (если бот не создан) или `/token` (если бот уже создан)
3. Следуйте инструкциям
4. Скопируйте токен в формате: `1234567890:ABCdefGHIjklMNOpqrsTUVwxyz-1234567890`

### Шаг 4: Перезапустите контейнер

После установки переменной окружения:

```bash
# Остановите контейнер
docker-compose down

# Запустите заново
docker-compose up -d

# Проверьте логи
docker-compose logs -f
```

### Шаг 5: Проверьте логи

После перезапуска в логах должно быть:
```
TelegramNotificationService initialized. Bot token configured: True
```

Если видите ошибку:
```
TelegramBot:Token is not configured or is empty
```

Это означает, что переменная окружения не установлена правильно.

## Формат токена

Токен должен быть в формате:
```
1234567890:ABCdefGHIjklMNOpqrsTUVwxyz-1234567890
```

Где:
- Первая часть (до `:`) - это ID бота (число)
- Вторая часть (после `:`) - это секретный ключ (строка из букв, цифр, дефисов и подчеркиваний)

## Проверка правильности токена

После установки токена проверьте, что он правильно загружается:

```bash
# В логах должно быть:
docker logs <container_id> | grep TelegramNotificationService
```

Должна быть строка:
```
TelegramNotificationService initialized. Bot token configured: True
```

Если видите `False` или ошибку - токен не установлен правильно.

