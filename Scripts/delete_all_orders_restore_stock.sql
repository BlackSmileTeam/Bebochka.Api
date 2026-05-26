-- =============================================================================
-- Удаление всех заказов и возврат товаров в каталог (остаток на складе).
--
-- Логика как в OrderService.DeleteOrderAsync:
--   • QuantityInStock увеличивается по позициям заказа;
--   • позиции из Telegram (TelegramCommentChatId IS NOT NULL) не трогаем —
--     при таких бронях остаток при создании заказа не списывался;
--   • для заказов уже в статусе «Отменен» остаток не возвращаем повторно
--     (он был возвращён при отмене).
--
-- Перед запуском: сделайте бэкап БД.
-- Проверка (без изменений): выполните только блок «Просмотр».
-- =============================================================================

-- ----- Просмотр -----
SELECT COUNT(*) AS orders_count FROM orders;
SELECT Status, COUNT(*) AS cnt FROM orders GROUP BY Status ORDER BY cnt DESC;

SELECT
    oi.ProductId,
    p.Name,
    SUM(oi.Quantity) AS qty_to_restore
FROM orderitems oi
INNER JOIN orders o ON o.Id = oi.OrderId
INNER JOIN products p ON p.Id = oi.ProductId
WHERE o.Status <> 'Отменен'
  AND oi.TelegramCommentChatId IS NULL
GROUP BY oi.ProductId, p.Name
ORDER BY oi.ProductId;

-- ----- Удаление (раскомментируйте после проверки) -----
-- MySQL Workbench: при Error 1175 (safe updates) скрипт временно отключает
-- sql_safe_updates в этой сессии и восстанавливает значение после COMMIT.
/*
START TRANSACTION;

SET @OLD_SQL_SAFE_UPDATES = @@SQL_SAFE_UPDATES;
SET SQL_SAFE_UPDATES = 0;

UPDATE products p
INNER JOIN (
    SELECT
        oi.ProductId,
        SUM(oi.Quantity) AS qty
    FROM orderitems oi
    INNER JOIN orders o ON o.Id = oi.OrderId
    WHERE o.Status <> 'Отменен'
      AND oi.TelegramCommentChatId IS NULL
    GROUP BY oi.ProductId
) agg ON agg.ProductId = p.Id
SET p.QuantityInStock = p.QuantityInStock + agg.qty
WHERE p.Id > 0;

-- Связанные строки удалятся каскадом (orderitems, orderstatushistories,
-- ordercustomerreviews с OrderId). Отзывы без заказа (OrderId IS NULL) остаются.
DELETE FROM orders WHERE Id > 0;

SET SQL_SAFE_UPDATES = @OLD_SQL_SAFE_UPDATES;

COMMIT;

SELECT COUNT(*) AS orders_left FROM orders;
*/

-- ----- Удалить только часть заказов (пример: по id) -----
/*
START TRANSACTION;

UPDATE products p
INNER JOIN (
    SELECT oi.ProductId, SUM(oi.Quantity) AS qty
    FROM orderitems oi
    INNER JOIN orders o ON o.Id = oi.OrderId
    WHERE o.Id IN (1, 2, 3)  -- <-- id заказов
      AND o.Status <> 'Отменен'
      AND oi.TelegramCommentChatId IS NULL
    GROUP BY oi.ProductId
) agg ON agg.ProductId = p.Id
SET p.QuantityInStock = p.QuantityInStock + agg.qty;

DELETE FROM orders WHERE Id IN (1, 2, 3);

COMMIT;
*/
