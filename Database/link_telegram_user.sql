-- Скрипт для связывания пользователя с Telegram User ID
-- Использование:
-- 1. Найдите ваш Telegram User ID (см. инструкцию ниже)
-- 2. Замените YOUR_TELEGRAM_USER_ID на ваш ID
-- 3. Замените YOUR_USERNAME на ваше имя пользователя (например, 'admin')
-- 4. Выполните скрипт

USE bebochka;

-- Вариант 1: Привязать по имени пользователя
-- UPDATE Users SET TelegramUserId = YOUR_TELEGRAM_USER_ID WHERE Username = 'YOUR_USERNAME';

-- Вариант 2: Привязать по ID пользователя (если знаете ID)
-- UPDATE Users SET TelegramUserId = YOUR_TELEGRAM_USER_ID WHERE Id = YOUR_USER_ID;

-- Пример для пользователя admin:
-- UPDATE Users SET TelegramUserId = 123456789 WHERE Username = 'admin';

-- Проверка результата
SELECT 
    Id,
    Username,
    TelegramUserId,
    IsActive,
    IsAdmin
FROM Users;

-- ============================================
-- Как получить Telegram User ID:
-- ============================================
-- 
-- Способ 1: Через бота @userinfobot
-- 1. Откройте Telegram
-- 2. Найдите бота @userinfobot
-- 3. Отправьте боту команду /start
-- 4. Бот покажет ваш User ID (например: 123456789)
--
-- Способ 2: Через бота @getmyid_bot
-- 1. Откройте Telegram
-- 2. Найдите бота @getmyid_bot
-- 3. Отправьте боту любое сообщение
-- 4. Бот ответит с вашим User ID
--
-- Способ 3: Через API (если есть доступ к вашему боту)
-- 1. Откройте Telegram
-- 2. Напишите вашему боту любое сообщение (например: /start)
-- 3. Проверьте логи бота или используйте getUpdates API
-- 4. В ответе будет поле "from": {"id": YOUR_USER_ID}
--
-- Способ 4: Через веб-версию Telegram (DevTools)
-- 1. Откройте https://web.telegram.org
-- 2. Откройте DevTools (F12)
-- 3. Перейдите в Network
-- 4. Отправьте сообщение вашему боту
-- 5. Найдите запрос и в ответе найдите "from": {"id": YOUR_USER_ID}

