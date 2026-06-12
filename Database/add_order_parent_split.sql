-- Частичная отправка: родительский заказ и подзаказы (найденные / не найденные позиции).
-- Выполнить на prod MySQL после деплоя API с поддержкой ParentOrderId.

ALTER TABLE orders
    ADD COLUMN ParentOrderId INT NULL AFTER CancellationReason;

ALTER TABLE orders
    ADD INDEX IX_orders_ParentOrderId (ParentOrderId);

ALTER TABLE orders
    ADD CONSTRAINT FK_orders_ParentOrderId
        FOREIGN KEY (ParentOrderId) REFERENCES orders (Id)
        ON DELETE RESTRICT;
