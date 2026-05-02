-- Поступления + локация товара (коробка/посылка) + закупка/прибыль
-- mysql -u ... -p bebochka < migration_incoming_shipments_and_product_location.sql

USE bebochka;

CREATE TABLE IF NOT EXISTS IncomingShipments (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(120) NOT NULL,
  WeightKg DECIMAL(10,3) NOT NULL DEFAULT 0,
  ItemCount INT NOT NULL DEFAULT 0,
  OrderedAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
  Profit DECIMAL(10,2) NULL,
  Notes VARCHAR(1000) NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  INDEX IX_IncomingShipments_CreatedAt (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS IncomingShipmentExpenses (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  IncomingShipmentId INT NOT NULL,
  Name VARCHAR(120) NOT NULL,
  Amount DECIMAL(10,2) NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX IX_IncomingShipmentExpenses_ShipmentId (IncomingShipmentId),
  CONSTRAINT FK_IncomingShipmentExpenses_IncomingShipments
    FOREIGN KEY (IncomingShipmentId) REFERENCES IncomingShipments (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET @col_box_exists := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'Products'
    AND COLUMN_NAME = 'BoxNumber'
);
SET @sql_col_box := IF(
  @col_box_exists = 0,
  'ALTER TABLE Products ADD COLUMN BoxNumber VARCHAR(50) NULL;',
  'SELECT ''Column BoxNumber already exists'';'
);
PREPARE stmt_col_box FROM @sql_col_box;
EXECUTE stmt_col_box;
DEALLOCATE PREPARE stmt_col_box;

SET @col_shipment_exists := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'Products'
    AND COLUMN_NAME = 'IncomingShipmentId'
);
SET @sql_col_shipment := IF(
  @col_shipment_exists = 0,
  'ALTER TABLE Products ADD COLUMN IncomingShipmentId INT NULL;',
  'SELECT ''Column IncomingShipmentId already exists'';'
);
PREPARE stmt_col_shipment FROM @sql_col_shipment;
EXECUTE stmt_col_shipment;
DEALLOCATE PREPARE stmt_col_shipment;

SET @fk_exists := (
  SELECT COUNT(*)
  FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE()
    AND TABLE_NAME = 'Products'
    AND CONSTRAINT_NAME = 'FK_Products_IncomingShipments'
);

SET @sql_fk := IF(
  @fk_exists = 0,
  'ALTER TABLE Products ADD CONSTRAINT FK_Products_IncomingShipments FOREIGN KEY (IncomingShipmentId) REFERENCES IncomingShipments(Id) ON DELETE SET NULL;',
  'SELECT ''FK already exists'';'
);
PREPARE stmt_fk FROM @sql_fk;
EXECUTE stmt_fk;
DEALLOCATE PREPARE stmt_fk;

SET @idx_exists := (
  SELECT COUNT(*)
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'Products'
    AND INDEX_NAME = 'IX_Products_IncomingShipmentId'
);
SET @sql_idx := IF(
  @idx_exists = 0,
  'CREATE INDEX IX_Products_IncomingShipmentId ON Products (IncomingShipmentId);',
  'SELECT ''Index already exists'';'
);
PREPARE stmt_idx FROM @sql_idx;
EXECUTE stmt_idx;
DEALLOCATE PREPARE stmt_idx;

SELECT 'Incoming shipments + expenses migration ready' AS message;
