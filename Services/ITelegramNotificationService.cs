namespace Bebochka.Api.Services;

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
    /// <returns>True if message was sent successfully</returns>
    Task<bool> SendMessageToChannelAsync(string message);
}

