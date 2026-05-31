-- Профиль: автофильтр каталога по детям + таблицы реферальной программы.
-- mysql -u USER -p bebochka < Database/migration_profile_autofilter_and_referrals.sql

USE bebochka;

-- AutoFilterByChildren на users
SET @users_tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'users'
  LIMIT 1
);

SET @col_exists := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = @users_tbl
    AND COLUMN_NAME = 'AutoFilterByChildren'
);

SET @sql := IF(
  @users_tbl IS NULL,
  'SELECT ''ERROR: table users not found'' AS Info',
  IF(
    @col_exists = 0,
    CONCAT('ALTER TABLE `', @users_tbl, '` ADD COLUMN AutoFilterByChildren TINYINT(1) NOT NULL DEFAULT 0'),
    'SELECT ''AutoFilterByChildren already exists'' AS Info'
  )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- userchildren (если ещё нет; имя в нижнем регистре для Linux MySQL)
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
      'CREATE TABLE userchildren (',
      '  Id INT AUTO_INCREMENT PRIMARY KEY,',
      '  UserId INT NOT NULL,',
      '  Name VARCHAR(100) NOT NULL,',
      '  DateOfBirth DATE NOT NULL,',
      '  ClothingSize VARCHAR(100) NOT NULL,',
      '  Gender VARCHAR(20) NOT NULL,',
      '  CreatedAt DATETIME NOT NULL,',
      '  UpdatedAt DATETIME NOT NULL,',
      '  INDEX IX_UserChildren_UserId (UserId),',
      '  CONSTRAINT FK_userchildren_users FOREIGN KEY (UserId) REFERENCES `', @users_tbl, '` (Id) ON DELETE CASCADE',
      ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
    ),
    'SELECT ''userchildren already exists'' AS Info'
  )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- DateOfBirth на users (для проверки возраста ребёнка относительно родителя)
SET @dob_col_exists := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = @users_tbl
    AND COLUMN_NAME = 'DateOfBirth'
);

SET @sql := IF(
  @users_tbl IS NULL,
  'SELECT ''ERROR: table users not found'' AS Info',
  IF(
    @dob_col_exists = 0,
    CONCAT('ALTER TABLE `', @users_tbl, '` ADD COLUMN DateOfBirth DATE NULL'),
    'SELECT ''DateOfBirth already exists'' AS Info'
  )
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Таблицы referralcodes и referrals: см. migration_user_children_and_referrals.sql
