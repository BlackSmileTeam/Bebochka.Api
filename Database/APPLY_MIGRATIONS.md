# Инструкция по применению миграций базы данных

## Проблема

На продакшене отсутствуют следующие колонки:
- `Products.PublishedAt` - дата публикации товара
- `Users.TelegramUserId` - идентификатор Telegram пользователя

## Решение

Примените SQL скрипт миграции для добавления недостающих колонок.

### Вариант 1: Через SSH подключение к серверу

1. Подключитесь к серверу по SSH:
```bash
ssh root@your-server-ip
```

2. Скопируйте файл `migrate_add_missing_columns.sql` на сервер (если его еще нет)

3. Выполните миграцию:
```bash
mysql -u root -p bebochka < /path/to/migrate_add_missing_columns.sql
```

Или подключитесь к MySQL и выполните скрипт:
```bash
mysql -u root -p
```
```sql
USE bebochka;
source /path/to/migrate_add_missing_columns.sql
```

### Вариант 2: Через Docker контейнер с MySQL

Если база данных работает в Docker контейнере:

1. Найдите ID контейнера MySQL:
```bash
docker ps | grep mysql
```

2. Скопируйте файл миграции в контейнер:
```bash
docker cp migrate_add_missing_columns.sql <container_id>:/tmp/migrate.sql
```

3. Выполните миграцию:
```bash
docker exec -i <container_id> mysql -u root -p<password> bebochka < /tmp/migrate.sql
```

### Вариант 3: Через phpMyAdmin или другой клиент MySQL

1. Откройте phpMyAdmin или другой клиент MySQL
2. Выберите базу данных `bebochka`
3. Откройте вкладку SQL
4. Скопируйте содержимое файла `migrate_add_missing_columns.sql`
5. Выполните скрипт

## Проверка

После применения миграции проверьте, что колонки добавлены:

```sql
USE bebochka;

-- Проверка колонки PublishedAt
SHOW COLUMNS FROM Products LIKE 'PublishedAt';

-- Проверка колонки TelegramUserId
SHOW COLUMNS FROM Users LIKE 'TelegramUserId';

-- Проверка индексов
SHOW INDEXES FROM Products WHERE Key_name = 'idx_products_published_at';
SHOW INDEXES FROM Users WHERE Key_name = 'idx_users_telegram_userid';
```

## Важно

- Скрипт безопасен для повторного выполнения - он проверяет наличие колонок перед добавлением
- Не забудьте сделать backup базы данных перед миграцией
- После миграции перезапустите приложение, чтобы изменения вступили в силу

## Откат миграции (если необходимо)

Если нужно откатить изменения:

```sql
USE bebochka;

-- Удаление индекса
DROP INDEX idx_products_published_at ON Products;

-- Удаление колонки PublishedAt
ALTER TABLE Products DROP COLUMN PublishedAt;

-- Удаление индекса
DROP INDEX idx_users_telegram_userid ON Users;

-- Удаление колонки TelegramUserId
ALTER TABLE Users DROP COLUMN TelegramUserId;
```

