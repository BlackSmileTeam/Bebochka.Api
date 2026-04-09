-- Миграция: авторизация, корзина по UserId, очередь сайта, OTP, CartAvailableAt
-- Совместимо с MySQL 5.7+ / 8.x / MariaDB (без ADD COLUMN IF NOT EXISTS).
--
-- Перед ALTER обязательно выберите базу приложения, например:
--   USE jfaq;
-- Если ошибка 1146 (таблица Products не существует) — сначала выполните
--   create_missing_tables_if_not_exist.sql
--
-- Выполняйте по одному блоку или целиком. Если столбец/индекс уже есть:
--   1060 Duplicate column name — пропустите этот ADD COLUMN
--   1061 Duplicate key name — пропустите этот CREATE INDEX
-- Таблица Users уже должна существовать; НЕ запускайте full_schema_mysql8.sql поверх неё.

SET NAMES utf8mb4;

-- === Users: Phone, GoogleSub ===
ALTER TABLE Users ADD COLUMN Phone VARCHAR(20) NULL;
ALTER TABLE Users ADD COLUMN GoogleSub VARCHAR(64) NULL;

-- Уникальность телефона/Google (несколько NULL в MySQL допустимо в UNIQUE)
CREATE UNIQUE INDEX IX_Users_Phone ON Users (Phone);
CREATE UNIQUE INDEX IX_Users_GoogleSub ON Users (GoogleSub);

-- === Products: отложенное открытие «В корзину» ===
ALTER TABLE Products ADD COLUMN CartAvailableAt DATETIME NULL;

-- === CartItems: привязка к аккаунту ===
ALTER TABLE CartItems ADD COLUMN UserId INT NULL;
CREATE INDEX IX_CartItems_UserId_ProductId ON CartItems (UserId, ProductId);

-- Внешний ключ (по желанию; на больших таблицах может занять время)
-- ALTER TABLE CartItems ADD CONSTRAINT FK_CartItems_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE;

-- === ReserveQueue: TelegramUserId может быть NULL; очередь сайта WebUserId ===
ALTER TABLE ReserveQueue MODIFY COLUMN TelegramUserId BIGINT NULL;
ALTER TABLE ReserveQueue ADD COLUMN WebUserId INT NULL;

-- Один пользователь — одна позиция в очереди на товар (WebUserId NOT NULL уникален в паре с ProductId)
CREATE UNIQUE INDEX IX_ReserveQueue_Product_WebUser ON ReserveQueue (ProductId, WebUserId);

-- ALTER TABLE ReserveQueue ADD CONSTRAINT FK_ReserveQueue_Users_WebUserId FOREIGN KEY (WebUserId) REFERENCES Users (Id) ON DELETE CASCADE;

-- === OTP входа по телефону ===
CREATE TABLE IF NOT EXISTS PhoneLoginOtps (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  PhoneE164 VARCHAR(20) NOT NULL,
  Code VARCHAR(10) NOT NULL,
  ExpiresAtUtc DATETIME NOT NULL,
  CreatedAtUtc DATETIME NOT NULL,
  INDEX IX_PhoneLoginOtps_PhoneE164 (PhoneE164)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
