-- Проверка схемы реферальной программы (только SELECT).
-- mysql -u USER -p bebochka < Database/check_referrals_schema.sql

USE bebochka;

SELECT 'Tables' AS Section;
SELECT TABLE_NAME, ENGINE, TABLE_ROWS
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND LOWER(TABLE_NAME) IN ('referralcodes', 'referrals', 'users', 'orders', 'telegramerrors')
ORDER BY TABLE_NAME;

SELECT 'referralcodes columns' AS Section;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_KEY
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND LOWER(TABLE_NAME) = 'referralcodes'
ORDER BY ORDINAL_POSITION;

SELECT 'referrals columns' AS Section;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_KEY
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND LOWER(TABLE_NAME) = 'referrals'
ORDER BY ORDINAL_POSITION;
