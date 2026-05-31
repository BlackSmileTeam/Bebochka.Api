-- Только просмотр: бренды из products, которых нет в Brands (без дублей).
-- mysql -u USER -p bebochka < Database/find_missing_brands.sql

USE bebochka;

SET @brands_tbl := (
  SELECT TABLE_NAME FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'brands' LIMIT 1
);

SET @products_tbl := (
  SELECT TABLE_NAME FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'products' LIMIT 1
);

SET @sql := IF(
  @brands_tbl IS NULL OR @products_tbl IS NULL,
  'SELECT ''ERROR: table Brands or products not found'' AS Info',
  CONCAT(
    'SELECT DISTINCT TRIM(p.Brand) AS BrandName, COUNT(*) AS ProductCount ',
    'FROM `', @products_tbl, '` p ',
    'WHERE p.Brand IS NOT NULL AND TRIM(p.Brand) <> '''' ',
    'AND NOT EXISTS (',
    '  SELECT 1 FROM `', @brands_tbl, '` b ',
    '  WHERE LOWER(TRIM(b.Name)) = LOWER(TRIM(p.Brand))',
    ') ',
    'GROUP BY LOWER(TRIM(p.Brand)), TRIM(p.Brand) ',
    'ORDER BY BrandName'
  )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
