# Инструкции по настройке базы данных MySQL

## Создание базы данных и структуры

### Вариант 1: Через SSH подключение к серверу MySQL

1. Подключитесь к серверу по SSH:
```bash
ssh username@your-server-ip
```

2. Подключитесь к MySQL как root пользователь:
```bash
mysql -u root -p
```

3. Выполните SQL скрипт для создания базы данных:
```sql
source /path/to/create_database.sql
```
Или скопируйте содержимое файла `create_database.sql` и выполните в MySQL консоли.

### Вариант 2: Через локальный MySQL

Если MySQL установлен локально:
```bash
mysql -u root -p < create_database.sql
```

## Создание пользователя MySQL через SSH

### Шаг 1: Подключение к серверу по SSH
```bash
ssh username@your-server-ip
```

### Шаг 2: Подключение к MySQL
```bash
mysql -u root -p
```
Введите пароль root пользователя MySQL.

### Шаг 3: Создание пользователя

Выполните команды из файла `create_user.sql` или выполните следующие команды:

```sql
-- Создание пользователя для удаленного доступа (облачный сервер БД)
CREATE USER IF NOT EXISTS 'bebochka_user'@'%' IDENTIFIED BY 'your_strong_password_here';

-- Предоставление прав на базу данных
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER ON bebochka.* TO 'bebochka_user'@'%';

-- Применение изменений
FLUSH PRIVILEGES;
```

**Важно:** Используется `'%'` вместо `'localhost'`, так как БД находится на облачном сервере и доступ будет удаленным.

### Шаг 4: Проверка прав пользователя
```sql
SHOW GRANTS FOR 'bebochka_user'@'%';
```

### Шаг 5: Тестирование подключения
Выйдите из MySQL и попробуйте подключиться новым пользователем:
```bash
mysql -u bebochka_user -p -h 89.104.67.36 bebochka
```

Или с указанием порта:
```bash
mysql -u bebochka_user -p -h 89.104.67.36 -P 3306 bebochka
```

## Альтернативный способ: Создание пользователя одной командой через SSH

Если у вас есть доступ к серверу по SSH, вы можете выполнить все команды одной строкой:

```bash
ssh username@89.104.67.36 "mysql -u root -p'your_root_password' <<EOF
CREATE DATABASE IF NOT EXISTS bebochka CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'bebochka_user'@'%' IDENTIFIED BY 'your_password';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER ON bebochka.* TO 'bebochka_user'@'%';
FLUSH PRIVILEGES;
EOF"
```

**ВНИМАНИЕ:** Этот способ небезопасен, так как пароль виден в истории команд. Используйте только для тестирования.

## Безопасность

1. **Используйте надежные пароли** - минимум 16 символов, включая буквы, цифры и специальные символы
2. **Ограничьте доступ по IP** - если возможно, создайте пользователя с доступом только с определенного IP:
   ```sql
   CREATE USER 'bebochka_user'@'your_app_server_ip' IDENTIFIED BY 'your_password';
   GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER ON bebochka.* TO 'bebochka_user'@'your_app_server_ip';
   FLUSH PRIVILEGES;
   ```
3. **Регулярно обновляйте пароли**
4. **Используйте SSL соединения** для удаленного доступа к БД
5. **Настройте firewall** - откройте порт 3306 только для IP адресов приложения

## Настройка приложения

После создания базы данных и пользователя, обновите строку подключения в `appsettings.json` или используйте переменные окружения:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=your_password_here;CharSet=utf8mb4;"
  }
}
```

**Важно:** Для продакшена используйте переменные окружения или секреты, не храните пароли в коде!

