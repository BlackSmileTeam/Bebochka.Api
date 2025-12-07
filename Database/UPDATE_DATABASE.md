# Инструкции по обновлению базы данных

## Добавление таблицы Users

Выполните SQL скрипт `add_users_table.sql` для создания таблицы пользователей:

```bash
mysql -u root -p bebochka < backend/Bebochka.Api/Database/add_users_table.sql
```

Или через SSH:

```bash
ssh username@89.104.67.36
mysql -u root -p bebochka < /path/to/add_users_table.sql
```

## Создание администратора

### Вариант 1: Автоматическое создание (рекомендуется)

При первом запуске приложения администратор создается автоматически:
- **Username:** `admin`
- **Password:** `Admin123!`
- **Email:** `admin@bebochka.com`

**ВАЖНО:** Смените пароль после первого входа!

### Вариант 2: Через API

После создания таблицы Users, создайте администратора через API:

```bash
POST http://localhost:5000/api/users
Content-Type: multipart/form-data

username=admin
password=YourSecurePassword123!
email=admin@bebochka.com
fullName=Администратор
```

**Требуется авторизация:** Используйте токен от существующего администратора или создайте пользователя через SQL скрипт.

### Вариант 3: Через SQL (не рекомендуется)

Если нужно создать через SQL, сначала сгенерируйте BCrypt хеш в приложении:

1. Запустите приложение
2. Используйте утилиту `PasswordHasher.HashPassword("YourPassword")`
3. Вставьте полученный хеш в SQL скрипт

**Пример SQL:**
```sql
USE bebochka;

INSERT INTO Users (Username, PasswordHash, Email, FullName, IsActive) 
VALUES (
    'admin',
    '$2a$11$YOUR_GENERATED_BCRYPT_HASH_HERE',
    'admin@bebochka.com',
    'Администратор',
    TRUE
);
```

## Проверка

После создания пользователя проверьте:

```sql
SELECT Id, Username, Email, FullName, IsActive, CreatedAt FROM Users;
```

## Безопасность

1. **Смените пароль по умолчанию** после первого входа
2. **Используйте надежные пароли** (минимум 12 символов)
3. **Не храните пароли в открытом виде** - всегда используйте BCrypt
4. **Ограничьте доступ** к таблице Users только для администраторов

