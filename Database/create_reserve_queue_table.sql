-- Очередь пользователей, написавших «беру» под постом, когда товар уже забронирован (следующий получит товар при снятии с заказа)
-- Выполнить на существующей БД

CREATE TABLE IF NOT EXISTS ReserveQueue (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  ProductId INT NOT NULL,
  ChannelId VARCHAR(50) NOT NULL,
  PostMessageId INT NOT NULL,
  TelegramUserId BIGINT NOT NULL,
  Username VARCHAR(255) NULL,
  FirstName VARCHAR(255) NULL,
  LastName VARCHAR(255) NULL,
  CustomerPhone VARCHAR(50) NULL,
  CommentChatId BIGINT NOT NULL,
  CommentMessageId INT NOT NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_reserve_queue_product (ProductId),
  INDEX idx_reserve_queue_channel_post (ChannelId, PostMessageId),
  FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
