-- SQL скрипт для создания администратора
-- ВАЖНО: Пароль должен быть захеширован с помощью BCrypt в приложении
-- 
-- Для генерации правильного BCrypt хеша:
-- 1. Запустите приложение
-- 2. Используйте API: POST /api/users с username, password, email, fullName
-- 3. Или используйте утилиту PasswordHasher в коде
--
-- Пароль по умолчанию: Admin123!
-- 
-- ВНИМАНИЕ: Хеш ниже - это пример. Замените на реальный хеш, сгенерированный через приложение!

USE bebochka;

-- Убедитесь, что таблица Users существует
-- Если нет, выполните сначала add_users_table.sql

-- Создание администратора
-- ПРИМЕЧАНИЕ: Используйте API для создания пользователя с правильным хешем
-- POST /api/users с параметрами:
--   username: admin
--   password: Admin123!
--   email: admin@bebochka.com
--   fullName: Администратор

-- Или вручную замените хеш ниже на реальный BCrypt хеш
INSERT INTO Users (Username, PasswordHash, Email, FullName, IsActive) 
VALUES (
    'admin',
    '$2a$11$KIXxZ8vJ5Z5Z5Z5Z5Z5Z5Oe5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z',
    'admin@bebochka.com',
    'Администратор',
    TRUE
) ON DUPLICATE KEY UPDATE 
    Username = Username;

-- Проверка созданного пользователя
SELECT Id, Username, Email, FullName, IsActive, CreatedAt FROM Users WHERE Username = 'admin';
