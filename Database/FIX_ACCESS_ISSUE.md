# Исправление ошибки "Access denied for user 'bebochka_user'@'IP'"

## Проблема

Ошибка: `MySqlConnector.MySqlException: "Access denied for user 'bebochka_user'@'178.70.181.170' (using password: YES)"`

Это означает, что:
1. Пользователь `bebochka_user` существует
2. Но либо пароль неверный, либо права доступа не настроены правильно

## Решение

### Вариант 1: Обновление пароля пользователя (если пароль неверный)

Если вы знаете, что пользователь существует, но пароль в строке подключения не совпадает:

```sql
-- Подключитесь к MySQL как root
mysql -u root -p

-- Выполните:
ALTER USER 'bebochka_user'@'%' IDENTIFIED BY 'НовыйПароль123!';
FLUSH PRIVILEGES;
```

Затем обновите пароль в строке подключения в:
- `appsettings.Development.json` (для локальной разработки)
- GitHub Secrets `DB_CONNECTION_STRING` (для продакшена)

### Вариант 2: Проверка и исправление прав доступа

1. **Подключитесь к MySQL как root:**
```bash
mysql -u root -p
```

2. **Проверьте существующих пользователей:**
```sql
SELECT User, Host FROM mysql.user WHERE User = 'bebochka_user';
```

3. **Проверьте права пользователя:**
```sql
SHOW GRANTS FOR 'bebochka_user'@'%';
```

4. **Если пользователь существует, но нет прав на базу данных, выполните:**
```sql
USE bebochka;
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER ON bebochka.* TO 'bebochka_user'@'%';
FLUSH PRIVILEGES;
```

5. **Если пользователь не существует или нужно пересоздать, выполните скрипт:**
```bash
mysql -u root -p < backend/Bebochka.Api/Database/fix_user_access.sql
```

**ВАЖНО:** Перед выполнением скрипта отредактируйте пароль в файле `fix_user_access.sql`!

### Вариант 3: Полное пересоздание пользователя

```sql
-- Удаление существующего пользователя
DROP USER IF EXISTS 'bebochka_user'@'%';
DROP USER IF EXISTS 'bebochka_user'@'localhost';

-- Создание нового пользователя
CREATE USER 'bebochka_user'@'%' IDENTIFIED BY 'ВашПароль123!';
CREATE USER 'bebochka_user'@'localhost' IDENTIFIED BY 'ВашПароль123!';

-- Предоставление прав
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER ON bebochka.* TO 'bebochka_user'@'%';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER ON bebochka.* TO 'bebochka_user'@'localhost';

-- Применение изменений
FLUSH PRIVILEGES;
```

## Проверка подключения

После исправления прав, проверьте подключение:

```bash
# С удаленного IP (замените на ваш IP)
mysql -u bebochka_user -p -h 89.104.67.36 bebochka

# С localhost
mysql -u bebochka_user -p -h localhost bebochka
```

## Настройка строки подключения

### Для локальной разработки

Обновите `appsettings.Development.json` или используйте User Secrets:

```bash
cd backend/Bebochka.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=ВашПароль;CharSet=utf8mb4;"
```

### Для продакшена

Обновите GitHub Secret `DB_CONNECTION_STRING`:

```
Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=ВашПароль;CharSet=utf8mb4;
```

## Частые проблемы

### 1. Пароль содержит специальные символы

Если пароль содержит `;`, `=`, `&` и другие символы, используйте одинарные кавычки в строке подключения:

```
Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password='P@ss;w0rd';CharSet=utf8mb4;
```

### 2. MySQL не разрешает удаленные подключения

Проверьте файл конфигурации MySQL (`/etc/mysql/mysql.conf.d/mysqld.cnf` или `/etc/my.cnf`):

```ini
bind-address = 0.0.0.0  # Разрешить подключения с любого IP
# или
# bind-address = 89.104.67.36  # Только с конкретного IP
```

После изменения перезапустите MySQL:
```bash
sudo systemctl restart mysql
```

### 3. Firewall блокирует подключения

Убедитесь, что порт 3306 открыт:

```bash
# Проверка открытых портов
sudo netstat -tlnp | grep 3306

# Если нужно открыть порт (Ubuntu/Debian)
sudo ufw allow 3306/tcp
```

## Диагностика

Если проблема сохраняется, выполните диагностику:

```sql
-- Проверка всех пользователей
SELECT User, Host FROM mysql.user;

-- Проверка прав конкретного пользователя
SHOW GRANTS FOR 'bebochka_user'@'%';

-- Проверка существования базы данных
SHOW DATABASES LIKE 'bebochka';

-- Проверка таблиц в базе данных
USE bebochka;
SHOW TABLES;
```

