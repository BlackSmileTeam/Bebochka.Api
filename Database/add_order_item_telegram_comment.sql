-- Добавляем в OrderItems поля для удаления комментария пользователя в Telegram при снятии товара с заказа
-- Выполнить на существующей БД: mysql ... < add_order_item_telegram_comment.sql

ALTER TABLE OrderItems
  ADD COLUMN TelegramCommentChatId BIGINT NULL COMMENT 'Chat ID комментария «беру» в Telegram' AFTER Quantity,
  ADD COLUMN TelegramCommentMessageId INT NULL COMMENT 'Message ID комментария в чате' AFTER TelegramCommentChatId;
