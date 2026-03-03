-- Привязка уже отправленного поста к товару (если при отправке не сохранились TelegramMessageId/TelegramChatId).
-- Пример (из лога): media group в канал, chat.id = -1003798506767, первый message_id = 108.
-- Выполнить: mysql -u root -p bebochka < fix_product_22_telegram_post_link.sql

USE bebochka;

UPDATE Products
SET TelegramChatId = '-1003798506767',
    TelegramMessageId = 108,
    UpdatedAt = UTC_TIMESTAMP()
WHERE Id = 22;

SELECT Id, Name, TelegramChatId, TelegramMessageId, UpdatedAt
FROM Products
WHERE Id = 22;

SELECT 'Product 22 post link updated.' AS message;

