-- Создание таблиц Bebochka, которых ещё нет (Users уже существует — не трогаем).
-- Столбец `Condition` в кавычках — иначе MySQL 1064 (зарезервированное слово).
-- Ошибка 1146 на ALTER TABLE Products значит: таблицы Products в этой базе просто нет.
--
-- 1) В Workbench выберите нужную схему (двойной клик по jfaq) ИЛИ выполните:
USE jfaq;

SET NAMES utf8mb4;

-- Товары (сразу с CartAvailableAt)
CREATE TABLE IF NOT EXISTS Products (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(200) NOT NULL,
  Brand VARCHAR(100) NULL,
  Description VARCHAR(1000) NULL,
  Price DECIMAL(10,2) NOT NULL,
  Size VARCHAR(20) NULL,
  Color VARCHAR(50) NULL,
  Images TEXT NOT NULL,
  TelegramFileIds TEXT NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  QuantityInStock INT NOT NULL DEFAULT 1,
  Gender VARCHAR(50) NULL,
  `Condition` VARCHAR(50) NULL,
  PublishedAt DATETIME NULL,
  CartAvailableAt DATETIME NULL,
  TelegramMessageId INT NULL,
  TelegramChatId VARCHAR(50) NULL,
  INDEX IX_Products_PublishedAt (PublishedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Корзина
CREATE TABLE IF NOT EXISTS CartItems (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  SessionId VARCHAR(255) NOT NULL,
  UserId INT NULL,
  ProductId INT NOT NULL,
  Quantity INT NOT NULL DEFAULT 1,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  INDEX IX_CartItems_Session_Product (SessionId, ProductId),
  INDEX IX_CartItems_User_Product (UserId, ProductId),
  CONSTRAINT FK_CartItems_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE,
  CONSTRAINT FK_CartItems_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Заказы
CREATE TABLE IF NOT EXISTS Orders (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  UserId INT NULL,
  OrderNumber VARCHAR(50) NOT NULL,
  CustomerName VARCHAR(255) NOT NULL,
  CustomerProfileLink VARCHAR(500) NULL,
  CustomerPhone VARCHAR(50) NOT NULL,
  CustomerEmail VARCHAR(255) NULL,
  CustomerAddress VARCHAR(500) NULL,
  DeliveryMethod VARCHAR(50) NULL,
  Comment TEXT NULL,
  TotalAmount DECIMAL(10,2) NOT NULL,
  Status VARCHAR(50) NOT NULL DEFAULT 'Ожидает оплату',
  DiscountType VARCHAR(20) DEFAULT 'None',
  FixedDiscountPercent INT NULL,
  Condition1ItemPercent INT NULL,
  Condition3ItemsPercent INT NULL,
  Condition5PlusPercent INT NULL,
  CancellationReason VARCHAR(500) NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  CancelledAt DATETIME NULL,
  UNIQUE KEY uk_orders_number (OrderNumber),
  INDEX IX_Orders_Status (Status),
  INDEX IX_Orders_Status_User (Status, UserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS OrderItems (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  OrderId INT NOT NULL,
  ProductId INT NOT NULL,
  ProductName VARCHAR(255) NOT NULL,
  ProductPrice DECIMAL(10,2) NOT NULL,
  Quantity INT NOT NULL,
  TelegramCommentChatId BIGINT NULL,
  TelegramCommentMessageId INT NULL,
  AddedToParcel TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE,
  CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS ReserveQueue (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  ProductId INT NOT NULL,
  ChannelId VARCHAR(50) NOT NULL,
  PostMessageId INT NOT NULL,
  TelegramUserId BIGINT NULL,
  WebUserId INT NULL,
  Username VARCHAR(255) NULL,
  FirstName VARCHAR(255) NULL,
  LastName VARCHAR(255) NULL,
  CustomerPhone VARCHAR(50) NULL,
  CommentChatId BIGINT NOT NULL DEFAULT 0,
  CommentMessageId INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  INDEX IX_ReserveQueue_ProductId (ProductId),
  INDEX IX_ReserveQueue_Channel_Post (ChannelId, PostMessageId),
  UNIQUE KEY uk_reserve_web (ProductId, WebUserId),
  CONSTRAINT FK_ReserveQueue_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE,
  CONSTRAINT FK_ReserveQueue_WebUser FOREIGN KEY (WebUserId) REFERENCES Users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS PhoneLoginOtps (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  PhoneE164 VARCHAR(20) NOT NULL,
  Code VARCHAR(10) NOT NULL,
  ExpiresAtUtc DATETIME NOT NULL,
  CreatedAtUtc DATETIME NOT NULL,
  INDEX IX_PhoneLoginOtps_Phone (PhoneE164)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Анонсы (Telegram); колонки ProductIds / CollageImages — JSON в виде TEXT (как в EF)
CREATE TABLE IF NOT EXISTS Announcements (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Message TEXT NOT NULL,
  ScheduledAt DATETIME NOT NULL,
  ProductIds TEXT NOT NULL DEFAULT ('[]'),
  CollageImages TEXT NOT NULL DEFAULT ('[]'),
  IsSent TINYINT(1) NOT NULL DEFAULT 0,
  SentAt DATETIME NULL,
  CreatedAt DATETIME NOT NULL DEFAULT (UTC_TIMESTAMP()),
  SentCount INT NOT NULL DEFAULT 0,
  INDEX idx_scheduled_at (ScheduledAt),
  INDEX idx_is_sent (IsSent)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Аудит согласий на обработку персональных данных (регистрация)
CREATE TABLE IF NOT EXISTS PersonalDataConsentLogs (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  UserId INT NOT NULL,
  ConsentKind VARCHAR(80) NOT NULL,
  AcceptedAtUtc DATETIME NOT NULL,
  IpAddress VARCHAR(45) NULL,
  UserAgent TEXT NULL,
  DeviceType VARCHAR(32) NULL,
  ExtraJson TEXT NULL,
  INDEX IX_PersonalDataConsentLogs_UserId (UserId),
  INDEX IX_PersonalDataConsentLogs_AcceptedAtUtc (AcceptedAtUtc),
  CONSTRAINT FK_PersonalDataConsentLogs_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Если Users создан вручную и без Phone/GoogleSub — после этого выполните строки из
-- migration_20250406_auth_cart_queue.sql (только ALTER/INDEX для Users, без повторного CREATE TABLE Users).
