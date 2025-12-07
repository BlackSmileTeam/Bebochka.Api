# Локальная разработка

## Настройка строки подключения к базе данных

Для локальной разработки есть два способа настройки строки подключения:

### Способ 1: Использование appsettings.Development.json (простой)

Отредактируйте файл `appsettings.Development.json` и замените `YOUR_PASSWORD_HERE` на реальный пароль:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=ВашПароль;CharSet=utf8mb4;"
  }
}
```

**⚠️ ВНИМАНИЕ:** Этот файл может попасть в репозиторий! Используйте этот способ только для тестирования.

### Способ 2: Использование User Secrets (рекомендуется)

User Secrets хранятся локально и не попадают в репозиторий. Это более безопасный способ.

#### Шаг 1: Установите User Secrets (если еще не установлен)

```bash
cd backend/Bebochka.Api
dotnet user-secrets init
```

#### Шаг 2: Добавьте строку подключения

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=ВашПароль;CharSet=utf8mb4;"
```

#### Шаг 3: Проверьте, что секрет добавлен

```bash
dotnet user-secrets list
```

Вы должны увидеть:
```
ConnectionStrings:DefaultConnection = Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=***;CharSet=utf8mb4;
```

#### Шаг 4: Удалите строку подключения из appsettings.Development.json

Убедитесь, что в `appsettings.Development.json` строка подключения либо отсутствует, либо пустая:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Формат строки подключения

### Базовый формат:
```
Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password=ВашПароль;CharSet=utf8mb4;
```

### Если пароль содержит специальные символы:
Если пароль содержит `;`, `=`, `&` и другие специальные символы, используйте одинарные кавычки:
```
Server=89.104.67.36;Port=3306;Database=bebochka;User=bebochka_user;Password='P@ssw0rd;123';CharSet=utf8mb4;
```

### Для локального MySQL (если БД на вашем компьютере):
```
Server=localhost;Port=3306;Database=bebochka;User=root;Password=ВашПароль;CharSet=utf8mb4;
```

## Проверка подключения

После настройки строки подключения запустите приложение:

```bash
cd backend/Bebochka.Api
dotnet run
```

Если подключение успешно, вы увидите:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

Если есть ошибки подключения, проверьте:
1. Правильность IP адреса сервера
2. Правильность имени пользователя и пароля
3. Что порт 3306 открыт (если БД на удаленном сервере)
4. Что база данных `bebochka` существует
5. Что пользователь `bebochka_user` имеет права доступа

## Создание пользователя MySQL (если нужно)

Если пользователь `bebochka_user` еще не создан, выполните на сервере MySQL:

```sql
CREATE DATABASE IF NOT EXISTS bebochka CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'bebochka_user'@'%' IDENTIFIED BY 'ВашПароль';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER ON bebochka.* TO 'bebochka_user'@'%';
FLUSH PRIVILEGES;
```

Подробнее см. `Database/README.md`

