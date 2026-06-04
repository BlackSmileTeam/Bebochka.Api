-- Тестовый товар: виден и доступен только администраторам (каталог, карточка, корзина).

ALTER TABLE `products`
    ADD COLUMN `IsTestProduct` TINYINT(1) NOT NULL DEFAULT 0 AFTER `IsKitDisplay`;

CREATE INDEX `IX_products_IsTestProduct` ON `products` (`IsTestProduct`);
