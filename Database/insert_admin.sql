-- Добавление администратора (логин: admin, пароль: Admin123!)
-- Пароль — BCrypt, как в приложении (BCrypt.Net). Замените USE на имя вашей базы.

SET NAMES utf8mb4;

-- Раскомментируйте и укажите свою БД:
-- USE bebochka;

-- Вариант A: вставить только если пользователя admin ещё нет
INSERT INTO Users (Username, PasswordHash, Email, FullName, IsActive, IsAdmin, CreatedAt)
SELECT
  'admin',
  '$2a$11$PLRd2w9YYplKvMVlzJ6ni.vFrgSXBs2pdDKfuRiMBmmxVEY.6m7Ge',
  'admin@bebochka.local',
  'Администратор',
  1,
  1,
  UTC_TIMESTAMP()
WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin');

-- Вариант B: создать или обновить admin (удобно, если запись уже есть)
-- INSERT INTO Users (Username, PasswordHash, Email, FullName, IsActive, IsAdmin, CreatedAt)
-- VALUES (
--   'admin',
--   '$2a$11$PLRd2w9YYplKvMVlzJ6ni.vFrgSXBs2pdDKfuRiMBmmxVEY.6m7Ge',
--   'admin@bebochka.local',
--   'Администратор',
--   1,
--   1,
--   UTC_TIMESTAMP()
-- )
-- ON DUPLICATE KEY UPDATE
--   PasswordHash = VALUES(PasswordHash),
--   IsAdmin = 1,
--   IsActive = 1,
--   Email = VALUES(Email),
--   FullName = VALUES(FullName);

-- Свой пароль: в C# выполните BCrypt.Net.BCrypt.HashPassword("ВашПароль")
-- и подставьте строку в PasswordHash вместо хеша выше.
