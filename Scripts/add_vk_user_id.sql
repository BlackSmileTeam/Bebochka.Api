-- VK OAuth: уникальный числовой id пользователя VK (после применения обновите модель/индексы при необходимости вручную)
ALTER TABLE users ADD COLUMN VkUserId BIGINT NULL;
CREATE UNIQUE INDEX IX_users_vkuserid ON users (VkUserId);
