-- Справочники цветов/состояний/нюансов, скидка на товаре, поле Nuance.
-- mysql -u USER -p bebochka < Database/migration_product_catalog_helpers.sql

CREATE TABLE IF NOT EXISTS productcolors (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(100) NOT NULL,
  UNIQUE KEY uk_productcolors_name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS productconditions (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(100) NOT NULL,
  UNIQUE KEY uk_productconditions_name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS productnuances (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(100) NOT NULL,
  UNIQUE KEY uk_productnuances_name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

SET @products_tbl := (
  SELECT TABLE_NAME FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'products' LIMIT 1
);

SET @col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @products_tbl AND COLUMN_NAME = 'Nuance'
);
SET @sql := IF(@products_tbl IS NULL, 'SELECT ''products not found'' AS Info',
  IF(@col = 0, CONCAT('ALTER TABLE `', @products_tbl, '` ADD COLUMN Nuance VARCHAR(100) NULL'), 'SELECT ''Nuance OK'' AS Info'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @products_tbl AND COLUMN_NAME = 'DiscountPercent'
);
SET @sql := IF(@products_tbl IS NULL, 'SELECT ''skip'' AS Info',
  IF(@col = 0, CONCAT('ALTER TABLE `', @products_tbl, '` ADD COLUMN DiscountPercent INT NULL'), 'SELECT ''DiscountPercent OK'' AS Info'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

INSERT IGNORE INTO productcolors (Name) VALUES
  ('Белый'), ('Черный'), ('Серый'), ('Бежевый'), ('Коричневый'), ('Красный'), ('Бордовый'),
  ('Розовый'), ('Оранжевый'), ('Желтый'), ('Зеленый'), ('Голубой'), ('Синий'), ('Фиолетовый'),
  ('Многоцветный'), ('Другой');

INSERT IGNORE INTO productconditions (Name) VALUES
  ('новая вещь'), ('состояние новой вещи'), ('очень хорошее'), ('отличное'), ('хорошее'), ('нормальное'), ('нюанс');

SELECT 'product catalog helpers migration done.' AS Info;
