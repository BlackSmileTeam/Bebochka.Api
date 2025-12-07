-- SQL скрипт для исправления прав доступа пользователя bebochka_user
-- Выполните этот скрипт от имени root пользователя MySQL

USE mysql;

-- Проверка существующих пользователей bebochka_user
SELECT User, Host FROM user WHERE User = 'bebochka_user';

-- Удаление всех существующих записей пользователя (если нужно пересоздать)
-- ВНИМАНИЕ: Раскомментируйте следующую строку только если нужно полностью пересоздать пользователя
-- DROP USER IF EXISTS 'bebochka_user'@'%';
-- DROP USER IF EXISTS 'bebochka_user'@'localhost';

-- Создание пользователя с доступом с любого IP (%)
-- ЗАМЕНИТЕ 'YourPassword123!' на реальный пароль
CREATE USER IF NOT EXISTS 'bebochka_user'@'%' IDENTIFIED BY 'YourPassword123!';

-- Предоставление всех необходимых прав на базу данных bebochka
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES, 
      CREATE TEMPORARY TABLES, LOCK TABLES, EXECUTE, CREATE VIEW, SHOW VIEW, 
      CREATE ROUTINE, ALTER ROUTINE, EVENT, TRIGGER ON bebochka.* TO 'bebochka_user'@'%';

-- Если нужно также дать доступ с localhost
CREATE USER IF NOT EXISTS 'bebochka_user'@'localhost' IDENTIFIED BY 'YourPassword123!';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, INDEX, ALTER, REFERENCES, 
      CREATE TEMPORARY TABLES, LOCK TABLES, EXECUTE, CREATE VIEW, SHOW VIEW, 
      CREATE ROUTINE, ALTER ROUTINE, EVENT, TRIGGER ON bebochka.* TO 'bebochka_user'@'localhost';

-- Применение изменений
FLUSH PRIVILEGES;

-- Проверка прав пользователя
SHOW GRANTS FOR 'bebochka_user'@'%';
SHOW GRANTS FOR 'bebochka_user'@'localhost';

-- Проверка подключения (выполните вне MySQL, в терминале)
-- mysql -u bebochka_user -p -h 89.104.67.36 bebochka

