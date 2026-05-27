-- Аудит принятия пользовательского соглашения и согласия на обработку ПДн.
-- mysql -u USER -p bebochka < Database/migration_personal_data_consent_logs.sql
--
-- Таблица: PersonalDataConsentLogs (в Linux/MySQL часто personaldataconsentlogs).
-- По одной регистрации обычно 2 строки: UserAgreement_v1 и PersonalDataProcessing_*.

USE bebochka;

SET @users_tbl := (
  SELECT TABLE_NAME
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'users'
  LIMIT 1
);

SET @consent_exists := (
  SELECT COUNT(*)
  FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE()
    AND LOWER(TABLE_NAME) = 'personaldataconsentlogs'
);

SET @sql := IF(
  @consent_exists = 0,
  CONCAT(
    'CREATE TABLE PersonalDataConsentLogs (',
    '  Id INT AUTO_INCREMENT PRIMARY KEY,',
    '  UserId INT NOT NULL,',
    '  ConsentKind VARCHAR(80) NOT NULL,',
    '  AcceptedAtUtc DATETIME NOT NULL,',
    '  IpAddress VARCHAR(45) NULL,',
    '  UserAgent TEXT NULL,',
    '  DeviceType VARCHAR(32) NULL,',
    '  ExtraJson TEXT NULL,',
    '  INDEX IX_PersonalDataConsentLogs_UserId (UserId),',
    '  INDEX IX_PersonalDataConsentLogs_AcceptedAtUtc (AcceptedAtUtc),',
    '  CONSTRAINT FK_PersonalDataConsentLogs_Users FOREIGN KEY (UserId) REFERENCES `', @users_tbl, '` (Id) ON DELETE RESTRICT',
    ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
  ),
  'SELECT ''personaldataconsentlogs already exists'' AS Info'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
