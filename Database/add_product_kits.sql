-- Комплекты одежды: таблица комплектов, связь товаров, поля корзины.
-- Запускать на production после деплоя API.

CREATE TABLE IF NOT EXISTS `product_kits` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `KitPrice` DECIMAL(10,2) NOT NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `products`
    ADD COLUMN `KitId` INT NULL AFTER `IncomingShipmentId`,
    ADD COLUMN `KitPartName` VARCHAR(200) NULL AFTER `KitId`,
    ADD COLUMN `KitPartSortOrder` INT NOT NULL DEFAULT 0 AFTER `KitPartName`,
    ADD COLUMN `IsKitDisplay` TINYINT(1) NOT NULL DEFAULT 0 AFTER `KitPartSortOrder`;

ALTER TABLE `products`
    ADD CONSTRAINT `FK_products_product_kits_KitId`
        FOREIGN KEY (`KitId`) REFERENCES `product_kits` (`Id`)
        ON DELETE SET NULL;

CREATE INDEX `IX_products_KitId` ON `products` (`KitId`);
CREATE INDEX `IX_products_IsKitDisplay` ON `products` (`IsKitDisplay`);

ALTER TABLE `cartitems`
    ADD COLUMN `KitId` INT NULL AFTER `ProductId`,
    ADD COLUMN `CartAddMode` VARCHAR(16) NULL AFTER `KitId`,
    ADD COLUMN `KitBundleKey` VARCHAR(36) NULL AFTER `CartAddMode`,
    ADD COLUMN `ChargedUnitPrice` DECIMAL(10,2) NULL AFTER `KitBundleKey`;

ALTER TABLE `cartitems`
    ADD CONSTRAINT `FK_cartitems_product_kits_KitId`
        FOREIGN KEY (`KitId`) REFERENCES `product_kits` (`Id`)
        ON DELETE SET NULL;

CREATE INDEX `IX_cartitems_KitId` ON `cartitems` (`KitId`);
CREATE INDEX `IX_cartitems_KitBundleKey` ON `cartitems` (`KitBundleKey`);
