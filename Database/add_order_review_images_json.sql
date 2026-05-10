-- Фото в отзывах (пути JSON) — выполните на своей БД при необходимости.
-- mysql -u ... -p bebochka_db < add_order_review_images_json.sql

ALTER TABLE ordercustomerreviews
  ADD COLUMN ReviewImagesJson TEXT NULL;
