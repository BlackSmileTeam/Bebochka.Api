-- Добавление новых полей в таблицу Products
ALTER TABLE Products 
ADD COLUMN IF NOT EXISTS QuantityInStock INT NOT NULL DEFAULT 1,
ADD COLUMN IF NOT EXISTS Gender VARCHAR(20) NULL COMMENT 'мальчик, девочка, унисекс',
ADD COLUMN IF NOT EXISTS Condition VARCHAR(20) NULL COMMENT 'новая, отличное, недостаток';

-- Создание таблицы для корзин пользователей (неоформленные заказы)
CREATE TABLE IF NOT EXISTS CartItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId VARCHAR(255) NOT NULL COMMENT 'ID сессии пользователя (из localStorage или cookie)',
    ProductId INT NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
    INDEX idx_session_product (SessionId, ProductId),
    INDEX idx_created_at (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Создание таблицы для оформленных заказов
CREATE TABLE IF NOT EXISTS Orders (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    OrderNumber VARCHAR(50) NOT NULL UNIQUE COMMENT 'Номер заказа',
    CustomerName VARCHAR(255) NOT NULL,
    CustomerPhone VARCHAR(50) NOT NULL,
    CustomerEmail VARCHAR(255) NULL,
    CustomerAddress TEXT NULL,
    DeliveryMethod VARCHAR(50) NULL COMMENT 'avito, yandex, ozon, 5post',
    Comment TEXT NULL,
    TotalAmount DECIMAL(10, 2) NOT NULL,
    Status VARCHAR(50) NOT NULL DEFAULT 'pending' COMMENT 'pending, confirmed, shipped, completed, cancelled',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_order_number (OrderNumber),
    INDEX idx_status (Status),
    INDEX idx_created_at (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Создание таблицы для товаров в заказах
CREATE TABLE IF NOT EXISTS OrderItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    OrderId INT NOT NULL,
    ProductId INT NOT NULL,
    ProductName VARCHAR(255) NOT NULL COMMENT 'Название товара на момент заказа',
    ProductPrice DECIMAL(10, 2) NOT NULL COMMENT 'Цена товара на момент заказа',
    Quantity INT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE RESTRICT,
    INDEX idx_order_id (OrderId),
    INDEX idx_product_id (ProductId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Обновление существующих записей, если они есть
UPDATE Products 
SET QuantityInStock = 1 
WHERE QuantityInStock IS NULL OR QuantityInStock = 0;

