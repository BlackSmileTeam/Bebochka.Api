namespace Bebochka.Api.Services;

/// <summary>
/// Result of sending a message to a Telegram channel (for linking post to product).
/// </summary>
public class ChannelSendResult
{
    public bool Success { get; set; }
    public int? MessageId { get; set; }
    public string? ChatId { get; set; }
}

/// <summary>
/// Service interface for sending notifications via Telegram
/// </summary>
public interface ITelegramNotificationService
{
    /// <summary>
    /// Sends a broadcast message to all Telegram bot users
    /// </summary>
    /// <param name="message">Message text to send</param>
    /// <returns>Number of users who received the message</returns>
    Task<int> SendBroadcastMessageAsync(string message);
    
    /// <summary>
    /// Sends a message to a specific Telegram user by chat ID
    /// </summary>
    /// <param name="chatId">Telegram chat ID</param>
    /// <param name="message">Message text to send</param>
    /// <returns>True if message was sent successfully</returns>
    Task<bool> SendMessageAsync(long chatId, string message);

    /// <summary>
    /// Deletes a message in a Telegram chat (e.g. user's "беру" comment when item is removed from order).
    /// </summary>
    Task<bool> DeleteMessageAsync(long chatId, int messageId);

    /// <summary>
    /// Sends one or more photos to a user by URLs (downloads then sends). Used e.g. for "В сборке" product cards.
    /// </summary>
    Task<bool> SendPhotosToUserByUrlsAsync(long chatId, List<string> imageUrls, string? caption = null);
    
    /// <summary>
    /// Sends a photo with caption to a specific Telegram user by chat ID
    /// </summary>
    /// <param name="chatId">Telegram chat ID</param>
    /// <param name="photoPath">Path to the photo file (relative to wwwroot)</param>
    /// <param name="caption">Optional caption for the photo</param>
    /// <returns>True if photo was sent successfully</returns>
    Task<bool> SendPhotoAsync(long chatId, string photoPath, string? caption = null);
    
    /// <summary>
    /// Sends a broadcast message with photos to all Telegram users
    /// </summary>
    /// <param name="message">Message text to send</param>
    /// <param name="photoPaths">List of photo paths to send</param>
    /// <returns>Number of users who received the message</returns>
    Task<int> SendBroadcastWithPhotosAsync(string message, List<string> photoPaths);
    
    /// <summary>
    /// Sends a message to a Telegram channel
    /// </summary>
    /// <param name="message">Message text to send</param>
    /// <returns>Result with Success and MessageId/ChatId for linking to product</returns>
    Task<ChannelSendResult> SendMessageToChannelAsync(string message, string? customEmojiId = null);
    
    /// <summary>
    /// Sends a message with photos to a Telegram channel.
    /// When telegramFileIds is provided (same count as imageUrls), photos are sent by file_id (no re-upload).
    /// </summary>
    /// <param name="message">Message text to send (caption for photos)</param>
    /// <param name="imageUrls">List of image URLs (used when telegramFileIds is not used)</param>
    /// <param name="telegramFileIds">Optional list of Telegram file_id; when set, skips download and upload</param>
    /// <returns>Result with Success and MessageId/ChatId for linking to product</returns>
    Task<ChannelSendResult> SendMessageToChannelWithPhotosAsync(string message, List<string> imageUrls, List<string>? telegramFileIds = null, string? customEmojiId = null);
    
    /// <summary>
    /// Uploads a photo to the configured storage chat and returns its file_id for later reuse.
    /// Requires TelegramBot:StorageChatId to be set (e.g. a private channel).
    /// </summary>
    /// <param name="imageBytes">Photo bytes</param>
    /// <param name="extension">File extension, e.g. .jpg</param>
    /// <returns>file_id or null if upload failed or StorageChatId is not set</returns>
    Task<string?> UploadPhotoToCacheAsync(byte[] imageBytes, string extension);
    
    /// <summary>
    /// Pre-caches product images in Telegram (upload to storage chat, save file_id).
    /// Call in background after creating/updating a product with future PublishedAt.
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="baseUrl">Base URL for building image URLs (e.g. https://yoursite.com)</param>
    Task PreCacheProductImagesAsync(int productId, string baseUrl);
}

