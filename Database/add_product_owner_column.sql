-- Добавляет системное поле владельца товара.
-- Допустимые значения на уровне приложения: "Аня", "Даша".

ALTER TABLE `products`
    ADD COLUMN `Owner` VARCHAR(50) NULL AFTER `BoxNumber`;

