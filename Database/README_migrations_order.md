# Порядок SQL-миграций (прод)

Выполняйте по очереди на базе `bebochka` (подставьте своего пользователя):

```bash
mysql -u USER -p bebochka < Database/migration_referrals.sql
mysql -u USER -p bebochka < Database/migration_profile_autofilter_and_referrals.sql
mysql -u USER -p bebochka < Database/migration_referral_checkout_discount.sql

Диагностика скидки приглашённого: `check_referral_user_orders.sql`
mysql -u USER -p bebochka < Database/migration_product_catalog_helpers.sql
mysql -u USER -p bebochka < Database/migration_favorites.sql
mysql -u USER -p bebochka < Database/migration_telegram_errors_image_count.sql
mysql -u USER -p bebochka < Database/migration_drop_duplicate_telegram_errors.sql
# (оставляет telegramerrors, удаляет дубликат TelegramErrors, если обе таблицы есть)
```

Если реферальные таблицы уже есть — шаг 1 можно пропустить.  
Если API с `DbSchemaBootstrap` уже запускался на этом сервере — часть колонок может уже существовать; скрипты идемпотентны.

После миграций перезапустите API.
