# Инструкции по развертыванию Backend API

## Необходимые GitHub Secrets

Добавьте следующие секреты в настройках репозитория GitHub (Settings → Secrets and variables → Actions):

**Подробная инструкция:** см. файл `GITHUB_SECRETS.md`

### Краткий список:

1. **SSH_HOST** = `89.104.67.36`
2. **SSH_USERNAME** = имя пользователя для SSH (например: `root`, `ubuntu`)
3. **SSH_PRIVATE_KEY** = приватный SSH ключ (полный ключ с BEGIN/END)
4. **SSH_PORT** = `22` (опционально)
5. **DB_CONNECTION_STRING** = строка подключения к MySQL:
   ```
   Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=your_secure_password;CharSet=utf8mb4;
   ```

## Создание таблиц в базе данных

### Подключение к серверу БД по SSH

```bash
ssh username@89.104.67.36
```

### Подключение к MySQL

```bash
mysql -u root -p
# или если у вас есть пользователь с правами root
mysql -u your_admin_user -p
```

**Важно:** БД находится на облачном сервере, поэтому пользователь создается с доступом `%` (удаленный доступ), а не `localhost`.

### Выполнение SQL команд

Скопируйте и выполните команды из файла `SQL_COMMANDS.md` или `Database/create_tables.sql`:

```sql
-- Создание базы данных
CREATE DATABASE IF NOT EXISTS bebochka CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Использование базы данных
USE bebochka;

-- Создание таблицы Products
CREATE TABLE IF NOT EXISTS Products (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    Brand VARCHAR(100) NULL,
    Description VARCHAR(1000) NULL,
    Price DECIMAL(10, 2) NOT NULL,
    Size VARCHAR(20) NULL,
    Color VARCHAR(50) NULL,
    Images TEXT NULL COMMENT 'JSON массив путей к изображениям',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_created_at (CreatedAt),
    INDEX idx_brand (Brand),
    INDEX idx_color (Color),
    INDEX idx_size (Size)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### Создание пользователя БД

После создания таблиц создайте пользователя для приложения:

```sql
-- Создание пользователя с удаленным доступом (% означает любой хост)
CREATE USER IF NOT EXISTS 'bebochka_user'@'%' IDENTIFIED BY 'your_secure_password_here';

-- Предоставление прав на базу данных
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER ON bebochka.* TO 'bebochka_user'@'%';

-- Применение изменений
FLUSH PRIVILEGES;

-- Проверка прав
SHOW GRANTS FOR 'bebochka_user'@'%';
```

**Важно:** 
- Используется `'%'` вместо `'localhost'`, так как БД на облачном сервере
- Замените `your_secure_password_here` на надежный пароль
- Этот пароль нужно будет указать в секрете `DB_CONNECTION_STRING`

### Или выполните через файл

```bash
mysql -u root -p < Database/create_tables.sql
```

## Развертывание через Docker Compose

На сервере создайте файл `.env`:

```env
DB_CONNECTION_STRING=Server=89.104.67.36;Port=3306;Database=bebochka;User=your_db_user;Password=your_db_password;CharSet=utf8mb4;
```

Затем выполните:

```bash
docker-compose up -d
```

## Ручное развертывание

### Backend

```bash
docker build -t bebochka-backend:latest -f Dockerfile .
docker run -d \
  --name bebochka-backend \
  --restart unless-stopped \
  -p 44315:44315 \
  -e ConnectionStrings__DefaultConnection="Server=89.104.67.36;Port=3306;Database=bebochka;User=your_db_user;Password=your_db_password;CharSet=utf8mb4;" \
  -v /opt/bebochka/uploads:/app/wwwroot/uploads \
  bebochka-backend:latest
```

## Проверка работы

- Backend API: http://89.104.67.36:44315/api
- Swagger: http://89.104.67.36:44315/swagger

