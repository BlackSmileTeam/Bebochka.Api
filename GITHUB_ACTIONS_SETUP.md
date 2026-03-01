# Настройка автоматического деплоя через GitHub Actions

## Описание

Создан GitHub Actions workflow файл `.github/workflows/deploy-backend.yml` в корне репозитория backend, который автоматически:
1. Собирает Docker образ приложения
2. Создает `.env` файл с секретами из GitHub Secrets
3. Копирует все необходимые файлы на сервер
4. Разворачивает приложение через docker-compose
5. Проверяет успешность деплоя

## Необходимые GitHub Secrets

Убедитесь, что в GitHub Secrets настроены все необходимые секреты:

1. **SSH_HOST** - IP адрес сервера (например: `89.104.67.36`)
2. **SSH_USERNAME** - имя пользователя SSH (например: `root`)
3. **SSH_PRIVATE_KEY** - приватный SSH ключ для подключения к серверу
4. **SSH_PORT** - порт SSH (опционально, по умолчанию 22)
5. **DB_CONNECTION_STRING** - строка подключения к MySQL
6. **TELEGRAM_BOT_TOKEN** - токен Telegram бота
7. **TELEGRAM_BOT_CHANNEL_ID** - ID или username Telegram канала (например: `@bebochkaTest` или `-1001234567890`)

## Как работает workflow

### Триггеры

Workflow запускается автоматически при:
- Push в ветки `main` или `master`
- Ручном запуске через GitHub Actions → Deploy Backend API → Run workflow

### Шаги выполнения

1. **Checkout code** - клонирует репозиторий
2. **Build Docker image** - собирает Docker образ приложения
3. **Save Docker image** - сохраняет образ в архив
4. **Copy files to server** - копирует docker-compose.yml, Dockerfile и образ на сервер
5. **Create .env file** - создает `.env` файл с секретами из GitHub Secrets
6. **Copy .env to server** - копирует `.env` файл на сервер
7. **Load Docker image and deploy** - загружает образ, останавливает старые контейнеры и запускает новые
8. **Verify deployment** - проверяет успешность деплоя и наличие токена

## Путь на сервере

По умолчанию файлы копируются в `/root/bebochka-backend`. Если нужно изменить путь:

1. Откройте `.github/workflows/deploy-backend.yml`
2. Найдите все упоминания `/root/bebochka-backend`
3. Замените на нужный путь

## Проверка после деплоя

После успешного деплоя на сервере будут:

- `/root/bebochka-backend/.env` - файл с секретами
- `/root/bebochka-backend/docker-compose.yml` - конфигурация Docker Compose
- `/root/bebochka-backend/wwwroot/uploads/` - директория для загрузки изображений

## Ручной запуск деплоя

1. Перейдите в репозиторий на GitHub
2. Откройте вкладку **Actions**
3. Выберите workflow **Deploy Backend API**
4. Нажмите **Run workflow**
5. Выберите ветку и нажмите **Run workflow**

## Устранение проблем

### Ошибка: "SSH connection failed"

- Проверьте правильность SSH_HOST, SSH_USERNAME, SSH_PRIVATE_KEY
- Убедитесь, что SSH ключ добавлен на сервер
- Проверьте, что порт SSH открыт

### Ошибка: "Docker image failed to load"

- Проверьте, что Docker установлен на сервере
- Убедитесь, что у пользователя есть права для работы с Docker

### Ошибка: "Telegram token not configured"

- Проверьте, что `TELEGRAM_BOT_TOKEN` добавлен в GitHub Secrets
- Убедитесь, что `.env` файл правильно скопирован на сервер
- Проверьте логи контейнера: `docker-compose logs backend`

### Ошибка: "Database connection failed"

- Проверьте, что `DB_CONNECTION_STRING` правильно настроен в GitHub Secrets
- Убедитесь, что база данных доступна с сервера
- Проверьте логи: `docker-compose logs backend`

## Безопасность

⚠️ **Важно:**
- Секреты хранятся только в GitHub Secrets, никогда не попадают в код
- `.env` файл создается на сервере и не коммитится в репозиторий
- При каждом деплое `.env` файл перезаписывается актуальными значениями
- Убедитесь, что `.env` добавлен в `.gitignore` (если он существует в репозитории)

## Обновление workflow

Если нужно добавить новые переменные окружения:

1. Добавьте их в GitHub Secrets
2. Обновите шаг "Create .env file" в `.github/workflows/deploy-backend.yml`:
```yaml
- name: Create .env file
  run: |
    cat > .env << EOF
    DB_CONNECTION_STRING=${{ secrets.DB_CONNECTION_STRING }}
    TELEGRAM_BOT_TOKEN=${{ secrets.TELEGRAM_BOT_TOKEN }}
    TELEGRAM_BOT_CHANNEL_ID=${{ secrets.TELEGRAM_BOT_CHANNEL_ID }}
    НОВАЯ_ПЕРЕМЕННАЯ=${{ secrets.НОВЫЙ_СЕКРЕТ }}
    EOF
```
3. Обновите `docker-compose.yml`, чтобы использовать новую переменную

