-- Добавляет хранение выбранного эмодзи канала для каждого пользователя

USE bebochka;

SET @col = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'bebochka' AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'ChannelCustomEmojiId');
SET @sql = IF(@col = 0, 'ALTER TABLE Users ADD COLUMN ChannelCustomEmojiId VARCHAR(32) NULL COMMENT ''Telegram custom_emoji_id для постов в канале'' AFTER TelegramUserId', 'SELECT ''ChannelCustomEmojiId exists''');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

