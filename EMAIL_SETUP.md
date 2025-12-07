# Настройка отправки email для заказов

## Конфигурация

Для отправки email при создании заказа необходимо настроить параметры SMTP в `appsettings.json` или через переменные окружения.

### Настройка в appsettings.json

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

### Настройка через переменные окружения (рекомендуется для продакшена)

```bash
Email__SmtpHost=smtp.gmail.com
Email__SmtpPort=587
Email__Username=your-email@gmail.com
Email__Password=your-app-password
Email__FromEmail=your-email@gmail.com
Email__ToEmail=sekisov@gmail.com
```

## Настройка Gmail

Если вы используете Gmail:

1. Включите двухфакторную аутентификацию в вашем Google аккаунте
2. Создайте пароль приложения:
   - Перейдите в [Настройки аккаунта Google](https://myaccount.google.com/)
   - Безопасность → Двухэтапная аутентификация → Пароли приложений
   - Создайте новый пароль приложения для "Почта" и "Другое устройство"
   - Используйте этот пароль в поле `Password` (не ваш обычный пароль!)

## Альтернативные SMTP серверы

### Яндекс.Почта
```json
{
  "Email": {
    "SmtpHost": "smtp.yandex.ru",
    "SmtpPort": "465",
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
    "SmtpPort": "465",
    "Username": "your-email@mail.ru",
    "Password": "your-password",
    "FromEmail": "your-email@mail.ru",
    "ToEmail": "sekisov@gmail.com"
  }
}
```

## Проверка работы

После настройки проверьте логи приложения при создании заказа. Должны появиться сообщения:
- `Attempting to send order email to sekisov@gmail.com...`
- `Order notification email sent successfully for order ORD-...`

Если возникают ошибки, проверьте:
1. Правильность учетных данных
2. Настройки файрвола (порт должен быть открыт)
3. Для Gmail - использование пароля приложения, а не обычного пароля

