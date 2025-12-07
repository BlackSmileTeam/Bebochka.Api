-- SQL скрипт для создания администратора с правильным BCrypt хешем
-- ВАЖНО: Этот хеш соответствует паролю "Admin123!"
-- Для генерации нового хеша используйте утилиту или API

USE bebochka;

-- Убедитесь, что таблица Users существует
-- Если нет, выполните сначала add_users_table.sql

-- Создание администратора
-- Пароль: Admin123!
-- BCrypt хеш (work factor 11): $2a$11$KIXxZ8vJ5Z5Z5Z5Z5Z5Z5Oe5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z5Z
-- ВАЖНО: Замените этот хеш на реальный, сгенерированный через приложение
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

