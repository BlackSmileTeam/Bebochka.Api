# Настройка токена Telegram бота при деплое через GitHub Actions

## Проблема

Токен `TELEGRAM_BOT_TOKEN` есть в GitHub Secrets, но не передается в Docker контейнер при деплое.

## Решение

### Вариант 1: GitHub Actions создает .env файл на сервере (рекомендуется)

Если у вас есть GitHub Actions workflow для деплоя, он должен создавать `.env` файл на сервере. Проверьте workflow файл (обычно `.github/workflows/deploy.yml`) и убедитесь, что он включает создание `.env` файла:

```yaml
- name: Create .env file on server
  run: |
    ssh ${{ secrets.SSH_USERNAME }}@${{ secrets.SSH_HOST }} << 'EOF'
    cd /path/to/Bebochka.Api
    cat > .env << EOL
    DB_CONNECTION_STRING=${{ secrets.DB_CONNECTION_STRING }}
    TELEGRAM_BOT_TOKEN=${{ secrets.TELEGRAM_BOT_TOKEN }}
    EOL
    EOF
```

### Вариант 2: Установка токена напрямую на сервере

Если workflow не создает `.env` файл автоматически, выполните на сервере:

```bash
# 1. Подключитесь к серверу
ssh root@89.104.67.36

# 2. Перейдите в каталог с docker-compose.yml
cd /path/to/Bebochka.Api

# 3. Проверьте, есть ли .env файл
ls -la .env

# 4. Если файла нет, создайте его
nano .env

# 5. Добавьте токен (получите его из GitHub Secrets или от BotFather)
TELEGRAM_BOT_TOKEN=ваш_токен_здесь

# 6. Если файл уже есть, добавьте или обновите строку с токеном
# Убедитесь, что строка имеет формат:
# TELEGRAM_BOT_TOKEN=1234567890:ABCdefGHIjklMNOpqrsTUVwxyz-1234567890

# 7. Сохраните файл (Ctrl+X, Y, Enter в nano)

# 8. Перезапустите контейнер
docker-compose down
docker-compose up -d

# 9. Проверьте логи
docker-compose logs -f | grep Telegram
```

### Вариант 3: Обновление GitHub Actions workflow

Если workflow существует, но не передает токен, обновите его. Найдите файл `.github/workflows/deploy.yml` (или похожий) и добавьте создание `.env` файла:

```yaml
- name: Deploy to server
  uses: appleboy/scp-action@master
  with:
    host: ${{ secrets.SSH_HOST }}
    username: ${{ secrets.SSH_USERNAME }}
    key: ${{ secrets.SSH_PRIVATE_KEY }}
    script: |
      cd /path/to/Bebochka.Api
      
      # Создаем или обновляем .env файл
      cat > .env << EOF
      DB_CONNECTION_STRING=${{ secrets.DB_CONNECTION_STRING }}
      TELEGRAM_BOT_TOKEN=${{ secrets.TELEGRAM_BOT_TOKEN }}
      EOF
      
      # Перезапускаем контейнер
      docker-compose down
      docker-compose up -d --build
```

### Вариант 4: Использование переменных окружения напрямую в docker-compose

Можно обновить workflow, чтобы он экспортировал переменные перед запуском docker-compose:

```yaml
- name: Deploy
  script: |
    export DB_CONNECTION_STRING="${{ secrets.DB_CONNECTION_STRING }}"
    export TELEGRAM_BOT_TOKEN="${{ secrets.TELEGRAM_BOT_TOKEN }}"
    docker-compose down
    docker-compose up -d --build
```

Но это менее надежно, так как переменные теряются после завершения сессии.

## Проверка после настройки

1. **Проверьте .env файл на сервере:**
```bash
ssh root@89.104.67.36
cd /path/to/Bebochka.Api
cat .env
```

Должна быть строка:
```
TELEGRAM_BOT_TOKEN=ваш_токен
```

2. **Проверьте переменные в контейнере:**
```bash
docker exec bebochka-backend env | grep TELEGRAM
```

3. **Проверьте логи:**
```bash
docker-compose logs | grep "TelegramNotificationService initialized"
```

Должно быть:
```
TelegramNotificationService initialized. Bot token configured: True
```

4. **Протестируйте отправку уведомления:**
- Установите товар на отложенную публикацию
- Дождитесь времени публикации
- Проверьте, что уведомление пришло в Telegram

## Если токен не работает

1. Убедитесь, что токен скопирован правильно из GitHub Secrets
2. Проверьте, что нет пробелов в начале/конце токена
3. Убедитесь, что `.env` файл находится в той же директории, что и `docker-compose.yml`
4. Проверьте права доступа к `.env` файлу (должен быть доступен для чтения)
5. Перезапустите контейнер после изменения `.env`

## Безопасность

⚠️ **Важно:**
- Не коммитьте `.env` файл в Git (должен быть в `.gitignore`)
- Храните токен только в GitHub Secrets для автоматического деплоя
- Используйте `.env` файл на сервере для локального управления

