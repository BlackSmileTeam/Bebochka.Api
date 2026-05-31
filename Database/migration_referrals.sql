-- Реферальная программа: таблицы referralcodes и referrals (нижний регистр).
-- Идемпотентно — можно запускать повторно.
--
-- mysql -u USER -p bebochka < Database/migration_referrals.sql
--
-- Если на сервере уже есть PascalCase-таблицы, сначала:
--   mysql -u USER -p bebochka < Database/migration_rename_tables_to_lowercase.sql
--
-- Проверка:
--   DESCRIBE referralcodes;
--   DESCRIBE referrals;

USE bebochka;

SET @users_tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'users'
  LIMIT 1
);

SET @referralcodes_exists := (
  SELECT COUNT(*)
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'referralcodes'
);

SET @sql := IF(
  @users_tbl IS NULL,
  'SELECT ''ERROR: table users not found'' AS Info',
  IF(
    @referralcodes_exists = 0,
    CONCAT(
      'CREATE TABLE referralcodes (',
      '  Id INT AUTO_INCREMENT PRIMARY KEY,',
      '  UserId INT NOT NULL,',
      '  Code VARCHAR(32) NOT NULL,',
      '  IsActive TINYINT(1) NOT NULL DEFAULT 1,',
      '  CreatedAt DATETIME NOT NULL,',
      '  UNIQUE KEY uk_referralcodes_code (Code),',
      '  UNIQUE KEY uk_referralcodes_user (UserId),',
      '  CONSTRAINT FK_ReferralCodes_Users FOREIGN KEY (UserId) REFERENCES `', @users_tbl, '` (Id) ON DELETE CASCADE',
      ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
    ),
    'SELECT ''referralcodes already exists'' AS Info'
  )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @referrals_exists := (
  SELECT COUNT(*)
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'referrals'
);

SET @referralcodes_tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'referralcodes'
  LIMIT 1
);

SET @orders_tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'orders'
  LIMIT 1
);

SET @sql := IF(
  @users_tbl IS NULL OR @referralcodes_tbl IS NULL,
  'SELECT ''ERROR: users or referralcodes not found'' AS Info',
  IF(
    @referrals_exists = 0,
    CONCAT(
      'CREATE TABLE referrals (',
      '  Id INT AUTO_INCREMENT PRIMARY KEY,',
      '  ReferrerUserId INT NOT NULL,',
      '  ReferredUserId INT NULL,',
      '  ReferralCodeId INT NOT NULL,',
      '  Status VARCHAR(30) NOT NULL DEFAULT ''Pending'',',
      '  CreatedAt DATETIME NOT NULL,',
      '  RegisteredAt DATETIME NULL,',
      '  FirstOrderId INT NULL,',
      '  RewardGrantedAt DATETIME NULL,',
      '  ReferrerRewardAmount DECIMAL(10,2) NULL,',
      '  ReferredRewardAmount DECIMAL(10,2) NULL,',
      '  INDEX IX_Referrals_ReferrerUserId (ReferrerUserId),',
      '  INDEX IX_Referrals_ReferralCodeId (ReferralCodeId),',
      '  INDEX IX_Referrals_ReferredUserId (ReferredUserId),',
      '  INDEX IX_Referrals_Status (Status),',
      '  CONSTRAINT FK_Referrals_ReferrerUsers FOREIGN KEY (ReferrerUserId) REFERENCES `', @users_tbl, '` (Id) ON DELETE RESTRICT,',
      '  CONSTRAINT FK_Referrals_ReferredUsers FOREIGN KEY (ReferredUserId) REFERENCES `', @users_tbl, '` (Id) ON DELETE SET NULL,',
      '  CONSTRAINT FK_Referrals_ReferralCodes FOREIGN KEY (ReferralCodeId) REFERENCES `', @referralcodes_tbl, '` (Id) ON DELETE RESTRICT',
      IF(
        @orders_tbl IS NULL,
        '',
        CONCAT(
          ', CONSTRAINT FK_Referrals_FirstOrders FOREIGN KEY (FirstOrderId) REFERENCES `',
          @orders_tbl,
          '` (Id) ON DELETE SET NULL'
        )
      ),
      ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
    ),
    'SELECT ''referrals already exists'' AS Info'
  )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT 'Done.' AS Info;
SHOW TABLES LIKE 'referral%';
