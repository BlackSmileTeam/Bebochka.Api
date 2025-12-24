# Быстрое исправление токена Telegram бота

## Проблема

Ошибка в логах:
```
TelegramBot:Token is not configured or is empty
```

## Решение (выберите один способ)

### Способ 1: Через .env файл (рекомендуется)

1. Подключитесь к серверу:
```bash
ssh root@89.104.67.36
```

2. Перейдите в каталог с docker-compose.yml:
```bash
cd /path/to/Bebochka.Api
# или где у вас находится docker-compose.yml
```

3. Создайте или отредактируйте файл `.env`:
```bash
nano .env
```

4. Добавьте строку с токеном:
```
TELEGRAM_BOT_TOKEN=ваш_токен_от_BotFather
```

Например:
```
TELEGRAM_BOT_TOKEN=1234567890:ABCdefGHIjklMNOpqrsTUVwxyz-1234567890
```

5. Сохраните файл (Ctrl+X, затем Y, затем Enter в nano)

6. Перезапустите контейнер:
```bash
docker-compose down
docker-compose up -d
```

7. Проверьте логи:
```bash
docker-compose logs -f | grep Telegram
```

Должно появиться:
```
TelegramNotificationService initialized. Bot token configured: True
```

### Способ 2: Экспортировать переменную перед запуском

```bash
# Подключитесь к серверу
ssh root@89.104.67.36

# Экспортируйте переменную
export TELEGRAM_BOT_TOKEN=ваш_токен_от_BotFather

# Перезапустите контейнер
cd /path/to/Bebochka.Api
docker-compose down
docker-compose up -d
```

⚠️ **Важно:** Этот способ работает только в текущей сессии. После перезагрузки сервера переменная пропадет. Используйте Способ 1 для постоянной настройки.

### Способ 3: Прямо в docker-compose.yml (не рекомендуется для production)

Отредактируйте `docker-compose.yml` и замените:
```yaml
- TelegramBot__Token=${TELEGRAM_BOT_TOKEN}
```

На:
```yaml
- TelegramBot__Token=ваш_токен_от_BotFather
```

⚠️ **Не рекомендуется:** Токен будет в открытом виде в файле.

## Как получить токен

1. Найдите бота [@BotFather](https://t.me/BotFather) в Telegram
2. Отправьте команду `/token` или `/newbot`
3. Следуйте инструкциям
4. Скопируйте токен (формат: `1234567890:ABCdefGHIjklMNOpqrsTUVwxyz-1234567890`)

## Проверка

После установки токена и перезапуска контейнера проверьте:

1. Логи инициализации:
```bash
docker-compose logs | grep "TelegramNotificationService initialized"
```

Должно быть: `Bot token configured: True`

2. Попробуйте опубликовать товар с отложенной публикацией

3. Проверьте логи отправки уведомлений:
```bash
docker-compose logs -f | grep "Publication notification"
```

## Если не работает

1. Убедитесь, что `.env` файл находится в той же директории, что и `docker-compose.yml`
2. Проверьте, что токен скопирован полностью, без пробелов в начале/конце
3. Проверьте права доступа к `.env` файлу (должен быть доступен для чтения)
4. Убедитесь, что вы перезапустили контейнер после изменения `.env`

