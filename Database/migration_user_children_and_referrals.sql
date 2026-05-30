-- Дети пользователя и заготовка под реферальную программу.
-- mysql -u USER -p bebochka < Database/migration_user_children_and_referrals.sql
--
-- На Linux (lower_case_table_names=1) физические имена: users, orders, userchildren и т.д.
-- FK ссылаются на реальное имя из information_schema.

USE bebochka;

SET @users_tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'users'
  LIMIT 1
);

SET @orders_tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'orders'
  LIMIT 1
);

-- UserChildren
SET @userchildren_exists := (
  SELECT COUNT(*)
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'userchildren'
);

SET @sql := IF(
  @users_tbl IS NULL,
  'SELECT ''ERROR: table users not found'' AS Info',
  IF(
    @userchildren_exists = 0,
    CONCAT(
      'CREATE TABLE UserChildren (',
      '  Id INT AUTO_INCREMENT PRIMARY KEY,',
      '  UserId INT NOT NULL,',
      '  Name VARCHAR(100) NOT NULL,',
      '  DateOfBirth DATE NOT NULL,',
      '  ClothingSize VARCHAR(100) NOT NULL,',
      '  Gender VARCHAR(20) NOT NULL,',
      '  CreatedAt DATETIME NOT NULL,',
      '  UpdatedAt DATETIME NOT NULL,',
      '  INDEX IX_UserChildren_UserId (UserId),',
      '  CONSTRAINT FK_UserChildren_Users FOREIGN KEY (UserId) REFERENCES `', @users_tbl, '` (Id) ON DELETE CASCADE',
      ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
    ),
    'SELECT ''userchildren already exists'' AS Info'
  )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ReferralCodes
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
      'CREATE TABLE ReferralCodes (',
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

-- Referrals
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

SET @first_order_fk := IF(
  @orders_tbl IS NULL,
  '',
  CONCAT(
    ', CONSTRAINT FK_Referrals_FirstOrders FOREIGN KEY (FirstOrderId) REFERENCES `', @orders_tbl, '` (Id) ON DELETE SET NULL'
  )
);

SET @sql := IF(
  @users_tbl IS NULL OR @referralcodes_tbl IS NULL,
  'SELECT ''ERROR: users or referralcodes table not found'' AS Info',
  IF(
    @referrals_exists = 0,
    CONCAT(
      'CREATE TABLE Referrals (',
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
      '  INDEX IX_Referrals_ReferredUserId (ReferredUserId),',
      '  INDEX IX_Referrals_ReferralCodeId (ReferralCodeId),',
      '  INDEX IX_Referrals_Status (Status),',
      '  CONSTRAINT FK_Referrals_ReferrerUsers FOREIGN KEY (ReferrerUserId) REFERENCES `', @users_tbl, '` (Id) ON DELETE RESTRICT,',
      '  CONSTRAINT FK_Referrals_ReferredUsers FOREIGN KEY (ReferredUserId) REFERENCES `', @users_tbl, '` (Id) ON DELETE SET NULL,',
      '  CONSTRAINT FK_Referrals_ReferralCodes FOREIGN KEY (ReferralCodeId) REFERENCES `', @referralcodes_tbl, '` (Id) ON DELETE RESTRICT',
      @first_order_fk,
      ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
    ),
    'SELECT ''referrals already exists'' AS Info'
  )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
