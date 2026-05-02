-- Полная схема Bebochka (MySQL 8+, utf8mb4) — только для НОВОЙ пустой БД под этот проект.
-- Столбец товара `Condition` в кавычках — слово CONDITION зарезервировано в MySQL.
--
-- CREATE DATABASE bebochka CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
-- USE bebochka;

SET NAMES utf8mb4;

CREATE TABLE Users (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Username VARCHAR(50) NOT NULL,
  PasswordHash VARCHAR(255) NOT NULL,
  Email VARCHAR(100) NULL,
  Phone VARCHAR(20) NULL,
  GoogleSub VARCHAR(64) NULL,
  FullName VARCHAR(100) NULL,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  IsAdmin TINYINT(1) NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  LastLoginAt DATETIME NULL,
  TelegramUserId BIGINT NULL,
  ChannelCustomEmojiId VARCHAR(64) NULL,
  UNIQUE KEY uk_users_username (Username),
  UNIQUE KEY uk_users_telegram (TelegramUserId),
  UNIQUE KEY uk_users_phone (Phone),
  UNIQUE KEY uk_users_google (GoogleSub)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE Products (
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
  BoxNumber VARCHAR(50) NULL,
  IncomingShipmentId INT NULL,
  INDEX IX_Products_PublishedAt (PublishedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IncomingShipments (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(120) NOT NULL,
  WeightKg DECIMAL(10,3) NOT NULL DEFAULT 0,
  ItemCount INT NOT NULL DEFAULT 0,
  OrderedAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
  Profit DECIMAL(10,2) NULL,
  Notes VARCHAR(1000) NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  INDEX IX_IncomingShipments_CreatedAt (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IncomingShipmentExpenses (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  IncomingShipmentId INT NOT NULL,
  Name VARCHAR(120) NOT NULL,
  Amount DECIMAL(10,2) NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  INDEX IX_IncomingShipmentExpenses_ShipmentId (IncomingShipmentId),
  CONSTRAINT FK_IncomingShipmentExpenses_IncomingShipments
    FOREIGN KEY (IncomingShipmentId) REFERENCES IncomingShipments (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE CartItems (
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

CREATE TABLE Orders (
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

CREATE TABLE OrderItems (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  OrderId INT NOT NULL,
  ProductId INT NOT NULL,
  ProductName VARCHAR(255) NOT NULL,
  ProductPrice DECIMAL(10,2) NOT NULL,
  Quantity INT NOT NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  TelegramCommentChatId BIGINT NULL,
  TelegramCommentMessageId INT NULL,
  AddedToParcel TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE,
  CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE ReserveQueue (
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

CREATE TABLE PhoneLoginOtps (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  PhoneE164 VARCHAR(20) NOT NULL,
  Code VARCHAR(10) NOT NULL,
  ExpiresAtUtc DATETIME NOT NULL,
  CreatedAtUtc DATETIME NOT NULL,
  INDEX IX_PhoneLoginOtps_Phone (PhoneE164)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

ALTER TABLE Products
  ADD CONSTRAINT FK_Products_IncomingShipments
  FOREIGN KEY (IncomingShipmentId) REFERENCES IncomingShipments (Id) ON DELETE SET NULL;

-- Упрощённые заглушки для остальных сущностей (Announcements, Brands, TelegramErrors) —
-- при необходимости скопируйте из существующей миграции или сгенерируйте из EF.
