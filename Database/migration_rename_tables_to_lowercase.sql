-- Переименование таблиц PascalCase -> lowercase на production (Linux MySQL).
-- Запускать один раз, если bootstrap создал таблицы с большой буквы.
--
-- mysql -u USER -p bebochka < Database/migration_rename_tables_to_lowercase.sql
--
-- Проверка до/после:
--   SHOW TABLES;

USE bebochka;

-- ReferralCodes -> referralcodes
SET @pascal := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ReferralCodes');
SET @lower := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'referralcodes');
SET @sql := IF(
  @pascal > 0 AND @lower = 0,
  'RENAME TABLE `ReferralCodes` TO `referralcodes`',
  IF(@pascal > 0 AND @lower > 0, 'SELECT ''WARN: ReferralCodes and referralcodes both exist — удалите пустую вручную'' AS Info', 'SELECT ''referralcodes OK'' AS Info')
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Referrals -> referrals
SET @pascal := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Referrals');
SET @lower := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'referrals');
SET @sql := IF(
  @pascal > 0 AND @lower = 0,
  'RENAME TABLE `Referrals` TO `referrals`',
  IF(@pascal > 0 AND @lower > 0, 'SELECT ''WARN: Referrals and referrals both exist — удалите пустую вручную'' AS Info', 'SELECT ''referrals OK'' AS Info')
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- TelegramErrors -> telegramerrors
SET @pascal := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TelegramErrors');
SET @lower := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'telegramerrors');
SET @sql := IF(
  @pascal > 0 AND @lower = 0,
  'RENAME TABLE `TelegramErrors` TO `telegramerrors`',
  IF(@pascal > 0 AND @lower > 0, 'SELECT ''WARN: TelegramErrors and telegramerrors both exist'' AS Info', 'SELECT ''telegramerrors OK'' AS Info')
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- PersonalDataConsentLogs -> personaldataconsentlogs
SET @pascal := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'PersonalDataConsentLogs');
SET @lower := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'personaldataconsentlogs');
SET @sql := IF(
  @pascal > 0 AND @lower = 0,
  'RENAME TABLE `PersonalDataConsentLogs` TO `personaldataconsentlogs`',
  IF(@pascal > 0 AND @lower > 0, 'SELECT ''WARN: PersonalDataConsentLogs and personaldataconsentlogs both exist'' AS Info', 'SELECT ''personaldataconsentlogs OK'' AS Info')
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- UserChildren -> userchildren (если создана с большой буквы)
SET @pascal := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'UserChildren');
SET @lower := (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'userchildren');
SET @sql := IF(
  @pascal > 0 AND @lower = 0,
  'RENAME TABLE `UserChildren` TO `userchildren`',
  IF(@pascal > 0 AND @lower > 0, 'SELECT ''WARN: UserChildren and userchildren both exist'' AS Info', 'SELECT ''userchildren OK'' AS Info')
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT 'Rename complete.' AS Info;
SELECT TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND LOWER(TABLE_NAME) IN (
    'referralcodes', 'referrals', 'telegramerrors',
    'personaldataconsentlogs', 'userchildren'
  )
ORDER BY TABLE_NAME;
