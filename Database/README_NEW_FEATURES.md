# Новые функции: Инвентарь и Заказы

## SQL Скрипты

Выполните SQL скрипт для добавления новых таблиц и полей:
```bash
mysql -u bebochka_user -p bebochka < Database/add_inventory_and_orders.sql
```

Или выполните вручную содержимое файла `Database/add_inventory_and_orders.sql`

## Настройка Email

Для отправки уведомлений о заказах на email `sekisov@gmail.com`, настройте в `appsettings.json`:

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

**Важно для Gmail:**
- Используйте "Пароль приложения" (App Password), а не обычный пароль
- Включите двухфакторную аутентификацию в Google аккаунте
- Создайте пароль приложения: https://myaccount.google.com/apppasswords

## Новые поля в товарах

1. **QuantityInStock** - Количество товаров в наличии (по умолчанию 1)
2. **Gender** - Пол (мальчик, девочка, унисекс)
3. **Condition** - Состояние (новая, отличное, недостаток)

## Новые таблицы

1. **CartItems** - Корзины пользователей (неоформленные заказы)
2. **Orders** - Оформленные заказы
3. **OrderItems** - Товары в заказах

## Логика работы

- При добавлении товара в корзину, количество блокируется для других пользователей
- При оформлении заказа:
  - Количество товара уменьшается на складе
  - Заказ сохраняется в БД
  - Отправляется email на `sekisov@gmail.com`
  - Товары удаляются из корзины пользователя

