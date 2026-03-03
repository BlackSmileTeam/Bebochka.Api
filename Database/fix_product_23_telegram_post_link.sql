-- Привязка уже отправленного поста к товару (если при отправке не сохранились TelegramMessageId/TelegramChatId).
-- Пример (из лога): media group в канал, chat.id = -1003798506767, первый message_id = 110.
-- Выполнить: mysql -u root -p bebochka < fix_product_23_telegram_post_link.sql

USE bebochka;

UPDATE Products
SET TelegramChatId = '-1003798506767',
    TelegramMessageId = 110,
    UpdatedAt = UTC_TIMESTAMP()
WHERE Id = 23;

SELECT Id, Name, TelegramChatId, TelegramMessageId, UpdatedAt
FROM Products
WHERE Id = 23;

SELECT 'Product 23 post link updated.' AS message;

-- Привязка уже отправленного поста к товару 23 (Свитшот).
-- Из лога: media group в канал @bebochkaTest, chat.id = -1003798506767, первый message_id = 110.
-- Выполнить: mysql -u root -p bebochka < fix_product_23_telegram_post_link.sql

USE bebochka;

UPDATE Products
SET TelegramChatId = '-1003798506767',
    TelegramMessageId = 110,
    UpdatedAt = UTC_TIMESTAMP()
WHERE Id = 23;

SELECT Id, Name, TelegramChatId, TelegramMessageId, UpdatedAt
FROM Products
WHERE Id = 23;

SELECT 'Product 23 post link updated.' AS message;
