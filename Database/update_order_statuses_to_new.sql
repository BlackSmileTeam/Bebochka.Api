-- Обновление старых значений статусов на новые (опционально)
-- Выполнить после add_order_discount_columns.sql при необходимости

USE bebochka;

UPDATE Orders SET Status = 'На доставку' WHERE Status = 'В пути';
UPDATE Orders SET Status = 'Отправлен' WHERE Status = 'Доставлен';

SELECT ROW_COUNT() AS updated_rows;
