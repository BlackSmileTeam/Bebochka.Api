-- Частичная отправка: родительский заказ и подзаказы (найденные / не найденные позиции).
-- Выполнить на prod MySQL после деплоя API с поддержкой ParentOrderId.
-- Workbench: выберите схему bebochka или выполните: mysql bebochka < add_order_parent_split.sql

USE bebochka;

ALTER TABLE orders
    ADD COLUMN ParentOrderId INT NULL AFTER CancellationReason;

ALTER TABLE orders
    ADD INDEX IX_orders_ParentOrderId (ParentOrderId);

ALTER TABLE orders
    ADD CONSTRAINT FK_orders_ParentOrderId
        FOREIGN KEY (ParentOrderId) REFERENCES orders (Id)
        ON DELETE RESTRICT;
