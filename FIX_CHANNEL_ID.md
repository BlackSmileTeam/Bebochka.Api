# Исправление проблемы с ChannelId

## Проблема
Сообщения не отправляются в Telegram канал, ошибка: "Failed to send message to channel"

## Причина
Скорее всего, `TELEGRAM_BOT_CHANNEL_ID` не передается в контейнер через переменную окружения.

## Решение

### 1. Проверьте GitHub Secrets

Убедитесь, что секрет `TELEGRAM_BOT_CHANNEL_ID` добавлен в GitHub Secrets:

1. Перейдите в репозиторий на GitHub
2. **Settings** → **Secrets and variables** → **Actions**
3. Проверьте, что есть секрет `TELEGRAM_BOT_CHANNEL_ID`
4. Значение должно быть: `@bebochkaTest` (или ваш ID канала)

### 2. Проверьте .env файл на сервере

Подключитесь к серверу и проверьте файл `.env`:

```bash
ssh username@89.104.67.36
cd /root/bebochka-backend  # или ваш путь
cat .env
```

Должно быть:
```
DB_CONNECTION_STRING=Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=...;CharSet=utf8mb4;
TELEGRAM_BOT_TOKEN=8413469940:AAERfxeHpD1zv7W8TjEp71JHkFNMU1EVi2Q
TELEGRAM_BOT_CHANNEL_ID=@bebochkaTest
```

### 3. Проверьте docker-compose.yml на сервере

Убедитесь, что в `docker-compose.yml` есть переменная:

```yaml
environment:
  - TelegramBot__ChannelId=${TELEGRAM_BOT_CHANNEL_ID}
```

### 4. Проверьте логи контейнера

```bash
docker-compose logs backend | grep -i "channel"
```

Должны увидеть:
- `Channel ID configured: True`
- `Channel ID value: @bebochkaTest` (или ваш ID)

Если видите `Channel ID configured: False` или `Channel ID value: EMPTY` - значит переменная не передается.

### 5. Если .env файл не содержит TELEGRAM_BOT_CHANNEL_ID

Если в `.env` файле нет `TELEGRAM_BOT_CHANNEL_ID`, нужно:

1. **Добавить секрет в GitHub Secrets** (если еще не добавлен)
2. **Обновить GitHub Actions workflow**, чтобы он создавал `.env` файл с этим секретом

Найдите файл `.github/workflows/deploy-backend.yml` и убедитесь, что в шаге "Create .env file" есть:

```yaml
- name: Create .env file
  run: |
    cat > .env << EOF
    DB_CONNECTION_STRING=${{ secrets.DB_CONNECTION_STRING }}
    TELEGRAM_BOT_TOKEN=${{ secrets.TELEGRAM_BOT_TOKEN }}
    TELEGRAM_BOT_CHANNEL_ID=${{ secrets.TELEGRAM_BOT_CHANNEL_ID }}
    EOF
```

### 6. Перезапустите контейнер

После исправления `.env` файла:

```bash
cd /root/bebochka-backend
docker-compose down
docker-compose up -d
docker-compose logs -f backend
```

### 7. Проверьте переменные окружения в контейнере

```bash
docker exec bebochka-backend env | grep TELEGRAM
```

Должны увидеть:
```
TelegramBot__ChannelId=@bebochkaTest
TelegramBot__Token=8413469940:AAERfxeHpD1zv7W8TjEp71JHkFNMU1EVi2Q
```

## Быстрое исправление (если секрет уже добавлен в GitHub)

Если секрет уже есть в GitHub Secrets, но не передается:

1. **Вручную создайте/обновите .env файл на сервере:**

```bash
ssh username@89.104.67.36
cd /root/bebochka-backend
cat >> .env << EOF
TELEGRAM_BOT_CHANNEL_ID=@bebochkaTest
EOF
```

2. **Перезапустите контейнер:**

```bash
docker-compose restart backend
```

3. **Проверьте логи:**

```bash
docker-compose logs backend | tail -20
```

## Проверка после исправления

После исправления попробуйте отправить сообщение в канал через админ-панель. В логах должно быть:

```
TelegramNotificationService initialized. Bot token configured: True, Channel ID configured: True, Channel ID value: @bebochkaTest
```

И сообщение должно успешно отправиться в канал.
