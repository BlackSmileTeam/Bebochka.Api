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
}

