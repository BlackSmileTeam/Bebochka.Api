-- MySQL 8+: локальный пользователь и база для Bebochka (выполнить от root).
-- После выполнения задайте тот же пароль в user secrets (см. комментарий в ответе ассистента).

CREATE DATABASE IF NOT EXISTS bebochka
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

-- Пользователь только с localhost (смените пароль!)
CREATE USER IF NOT EXISTS 'bebochka'@'localhost' IDENTIFIED BY 'BebochkaLocal1!';

GRANT ALL PRIVILEGES ON bebochka.* TO 'bebochka'@'localhost';
FLUSH PRIVILEGES;

-- Если приложение подключается по TCP с 127.0.0.1, иногда нужен отдельный хост:
-- CREATE USER IF NOT EXISTS 'bebochka'@'127.0.0.1' IDENTIFIED BY 'BebochkaLocal1!';
-- GRANT ALL PRIVILEGES ON bebochka.* TO 'bebochka'@'127.0.0.1';
-- FLUSH PRIVILEGES;
