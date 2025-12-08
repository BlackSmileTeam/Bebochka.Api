# Настройка отправки email уведомлений

## Конфигурация

Для отправки email уведомлений о новых заказах необходимо настроить параметры в `appsettings.json`:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromEmail": "your-email@gmail.com",
    "ToEmail": "sekisov@gmail.com"
  }
}
```

## Настройка для Gmail

1. **Включите двухфакторную аутентификацию** в вашем Google аккаунте
2. **Создайте пароль приложения:**
   - Перейдите в [Настройки аккаунта Google](https://myaccount.google.com/)
   - Выберите "Безопасность"
   - В разделе "Вход в аккаунт Google" найдите "Пароли приложений"
   - Создайте новый пароль приложения для "Почта" и "Другое устройство"
   - Используйте этот пароль в `Email:Password`

3. **Настройте параметры:**
   - `SmtpHost`: `smtp.gmail.com`
   - `SmtpPort`: `587`
   - `Username`: ваш email адрес Gmail
   - `Password`: пароль приложения (не ваш обычный пароль!)
   - `FromEmail`: ваш email адрес (обычно совпадает с Username)
   - `ToEmail`: `sekisov@gmail.com` (адрес для получения уведомлений)

## Настройка для других почтовых сервисов

### Yandex Mail
```json
{
  "Email": {
    "SmtpHost": "smtp.yandex.ru",
    "SmtpPort": "587",
    "Username": "your-email@yandex.ru",
    "Password": "your-password",
    "FromEmail": "your-email@yandex.ru",
    "ToEmail": "sekisov@gmail.com"
  }
}
```

### Mail.ru
```json
{
  "Email": {
    "SmtpHost": "smtp.mail.ru",
    "SmtpPort": "587",
    "Username": "your-email@mail.ru",
    "Password": "your-password",
    "FromEmail": "your-email@mail.ru",
    "ToEmail": "sekisov@gmail.com"
  }
}
```

## Проверка работы

После настройки проверьте логи приложения при создании заказа. Вы должны увидеть сообщения:
- `Attempting to send order email to sekisov@gmail.com...`
- `SMTP connection established. Authenticating...`
- `SMTP authentication successful. Sending email...`
- `Order notification email sent successfully for order ORD-...`

Если возникают ошибки, проверьте логи для получения подробной информации.

## Безопасность

⚠️ **ВАЖНО:** Не храните пароли в открытом виде в `appsettings.json` в production!

Для production используйте:
- **User Secrets** (для локальной разработки)
- **Environment Variables** (для Docker/сервера)
- **Azure Key Vault** или другие системы управления секретами

### Использование User Secrets (локально)

```bash
cd backend/Bebochka.Api
dotnet user-secrets set "Email:Username" "your-email@gmail.com"
dotnet user-secrets set "Email:Password" "your-app-password"
dotnet user-secrets set "Email:FromEmail" "your-email@gmail.com"
```

### Использование Environment Variables (Docker)

```bash
docker run -e Email__Username="your-email@gmail.com" \
           -e Email__Password="your-app-password" \
           -e Email__FromEmail="your-email@gmail.com" \
           ...
```

Или в `docker-compose.yml`:
```yaml
environment:
  - Email__Username=your-email@gmail.com
  - Email__Password=your-app-password
  - Email__FromEmail=your-email@gmail.com
```

