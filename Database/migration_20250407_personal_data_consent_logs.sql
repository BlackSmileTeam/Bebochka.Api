-- Таблица аудита согласий на обработку ПДн (для уже существующей БД).
USE bebochka;

CREATE TABLE IF NOT EXISTS PersonalDataConsentLogs (
  Id INT AUTO_INCREMENT PRIMARY KEY,
  UserId INT NOT NULL,
  ConsentKind VARCHAR(80) NOT NULL,
  AcceptedAtUtc DATETIME NOT NULL,
  IpAddress VARCHAR(45) NULL,
  UserAgent TEXT NULL,
  DeviceType VARCHAR(32) NULL,
  ExtraJson TEXT NULL,
  INDEX IX_PersonalDataConsentLogs_UserId (UserId),
  INDEX IX_PersonalDataConsentLogs_AcceptedAtUtc (AcceptedAtUtc),
  CONSTRAINT FK_PersonalDataConsentLogs_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
