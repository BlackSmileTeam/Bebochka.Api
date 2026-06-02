-- Favorites storage for authorized users
-- Safe to run multiple times

CREATE TABLE IF NOT EXISTS `user_favorite_products` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `UserId` INT NOT NULL,
  `ProductId` INT NOT NULL,
  `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_user_favorite_products_user_product` (`UserId`, `ProductId`),
  KEY `IX_user_favorite_products_created_at` (`CreatedAt`),
  KEY `IX_user_favorite_products_product_id` (`ProductId`),
  CONSTRAINT `FK_user_favorite_products_users_UserId`
    FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_user_favorite_products_products_ProductId`
    FOREIGN KEY (`ProductId`) REFERENCES `products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
