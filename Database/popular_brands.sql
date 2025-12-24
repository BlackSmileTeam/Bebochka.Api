-- SQL скрипт для добавления популярных брендов детской одежды
-- Этот скрипт создает таблицу Brands и заполняет её известными брендами

USE bebochka;

-- Создание таблицы Brands (если не существует)
CREATE TABLE IF NOT EXISTS Brands (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL UNIQUE,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Очистка таблицы перед заполнением (опционально, раскомментируйте если нужно)
-- TRUNCATE TABLE Brands;

-- Добавление популярных брендов детской одежды
INSERT INTO Brands (Name) VALUES
-- Международные премиум бренды
('Zara Kids'),
('H&M Kids'),
('Gap Kids'),
('Next'),
('Uniqlo Kids'),
('C&A Kids'),
('Benetton Kids'),
('Tommy Hilfiger Kids'),
('Ralph Lauren Kids'),
('Calvin Klein Kids'),
('Armani Junior'),
('Versace Junior'),
('Dolce & Gabbana Kids'),
('Gucci Kids'),
('Burberry Kids'),
('Chloe Kids'),
('Fendi Kids'),
('Moncler Enfant'),
('Kenzo Kids'),
('Boss Kids'),

-- Европейские бренды
('Okaidi'),
('Obaibi'),
('Bonpoint'),
('Jacadi'),
('Catimini'),
('Petit Bateau'),
('Du Pareil au Même'),
('Kickers'),
('GEOX Kids'),
('Lacoste Kids'),
('French Connection Kids'),
('Mango Kids'),
('Desigual Kids'),
('Primark Kids'),

-- Российские и СНГ бренды
('Gloria Jeans Kids'),
('Sela Kids'),
('Ослик'),
('Kira Plastinina Kids'),
('Лукоморье'),
('Мишка'),

-- Скандинавские бренды
('Marimekko Kids'),
('H&M Divided Kids'),
('COS Kids'),
('& Other Stories Kids'),
('Arket Kids'),
('Acne Studios Kids'),

-- Спортивные бренды для детей
('Nike Kids'),
('Adidas Kids'),
('Puma Kids'),
('Reebok Kids'),
('New Balance Kids'),
('Converse Kids'),
('Vans Kids'),
('Fila Kids'),

-- Детские специализированные бренды
('Carter\'s'),
('OshKosh B\'gosh'),
('Gymboree'),
('Old Navy Kids'),
('The Children\'s Place'),
('Justice'),
('Crazy 8'),
('Pumpkin Patch'),
('Janie and Jack'),

-- Другие популярные бренды
('Superdry Kids'),
('Vero Moda Kids'),
('Only Kids'),
('Selected Kids'),
('Jack & Jones Junior'),
('Esprit Kids'),
('Lindex Kids'),
('Lagerfeld Kids'),
('Karl Lagerfeld Kids')
ON DUPLICATE KEY UPDATE Name=Name;

-- Проверка количества добавленных брендов
SELECT COUNT(*) as TotalBrands FROM Brands;

-- Вывод всех брендов
SELECT * FROM Brands ORDER BY Name;

