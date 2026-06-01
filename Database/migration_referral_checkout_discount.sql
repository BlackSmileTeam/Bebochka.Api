-- Скидка 10% в корзине: фиксация использования по referrals и orders.
-- mysql -u USER -p bebochka < Database/migration_referral_checkout_discount.sql

SET @referrals_tbl := (
  SELECT TABLE_NAME FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'referrals' LIMIT 1
);

SET @orders_tbl := (
  SELECT TABLE_NAME FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'orders' LIMIT 1
);

SET @col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @referrals_tbl AND COLUMN_NAME = 'ReferredDiscountOrderId'
);
SET @sql := IF(@referrals_tbl IS NULL, 'SELECT ''referrals not found'' AS Info',
  IF(@col = 0, CONCAT('ALTER TABLE `', @referrals_tbl, '` ADD COLUMN ReferredDiscountOrderId INT NULL'), 'SELECT ''ReferredDiscountOrderId OK'' AS Info'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @referrals_tbl AND COLUMN_NAME = 'ReferrerDiscountOrderId'
);
SET @sql := IF(@referrals_tbl IS NULL, 'SELECT ''skip'' AS Info',
  IF(@col = 0, CONCAT('ALTER TABLE `', @referrals_tbl, '` ADD COLUMN ReferrerDiscountOrderId INT NULL'), 'SELECT ''ReferrerDiscountOrderId OK'' AS Info'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @orders_tbl AND COLUMN_NAME = 'ReferralId'
);
SET @sql := IF(@orders_tbl IS NULL, 'SELECT ''orders not found'' AS Info',
  IF(@col = 0, CONCAT('ALTER TABLE `', @orders_tbl, '` ADD COLUMN ReferralId INT NULL'), 'SELECT ''ReferralId OK'' AS Info'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @orders_tbl AND COLUMN_NAME = 'ReferralDiscountKind'
);
SET @sql := IF(@orders_tbl IS NULL, 'SELECT ''skip'' AS Info',
  IF(@col = 0, CONCAT('ALTER TABLE `', @orders_tbl, '` ADD COLUMN ReferralDiscountKind VARCHAR(20) NULL'), 'SELECT ''ReferralDiscountKind OK'' AS Info'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT 'Done.' AS Info;
