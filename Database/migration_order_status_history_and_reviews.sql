-- История статусов заказов и отзывы клиентов после «Получен»
-- mysql -u ... -p bebochka < migration_order_status_history_and_reviews.sql

USE bebochka;

CREATE TABLE IF NOT EXISTS OrderStatusHistories (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  OrderId INT NOT NULL,
  Status VARCHAR(50) NOT NULL,
  ChangedAtUtc DATETIME NOT NULL,
  ChangedByUserId INT NULL,
  INDEX IX_OrderStatusHistories_OrderId (OrderId),
  INDEX IX_OrderStatusHistories_ChangedAt (ChangedAtUtc),
  CONSTRAINT FK_OrderStatusHistories_Orders FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE,
  CONSTRAINT FK_OrderStatusHistories_Users FOREIGN KEY (ChangedByUserId) REFERENCES Users (Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS OrderCustomerReviews (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  OrderId INT NOT NULL,
  UserId INT NOT NULL,
  Rating INT NULL,
  Comment TEXT NULL,
  CreatedAtUtc DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP()),
  UNIQUE KEY uk_order_customer_review_order (OrderId),
  INDEX IX_OrderCustomerReviews_UserId (UserId),
  CONSTRAINT FK_OrderCustomerReviews_Orders FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE,
  CONSTRAINT FK_OrderCustomerReviews_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SELECT 'OrderStatusHistories and OrderCustomerReviews ready' AS message;
