using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Bebochka.Api.Data;

namespace Bebochka.Api.Services;

/// <summary>
/// Service for sending notifications via Telegram Bot API
/// </summary>
public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly AppDbContext _context;
    private readonly string _botToken;
    private readonly string _botApiUrl;

    public TelegramNotificationService(
        HttpClient httpClient,
        ILogger<TelegramNotificationService> logger,
        IConfiguration configuration,
        AppDbContext context)
    {
        _httpClient = httpClient;
        _logger = logger;
        _context = context;
        
        var tokenFromConfig = configuration["TelegramBot:Token"];
        if (string.IsNullOrWhiteSpace(tokenFromConfig))
        {
            _logger.LogError("TelegramBot:Token is not configured or is empty");
            throw new InvalidOperationException("TelegramBot:Token not found in configuration or is empty");
        }
        
        _botToken = tokenFromConfig;
        _botApiUrl = $"https://api.telegram.org/bot{_botToken}";
        
        // Log token presence (but not the actual token for security)
        _logger.LogInformation("TelegramNotificationService initialized. Bot token configured: {TokenPresent}", !string.IsNullOrEmpty(_botToken));
    }

    /// <summary>
    /// Sends a broadcast message to all Telegram bot users
    /// Uses Telegram User IDs from the Users table as chat IDs (for private chats, chat ID = user ID)
    /// </summary>
    public async Task<int> SendBroadcastMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Attempted to send empty broadcast message");
            return 0;
        }

        // Get all users who have Telegram User IDs (they have interacted with the bot)
        var telegramUsers = await _context.Users
            .Where(u => u.TelegramUserId != null && u.IsActive)
            .Select(u => u.TelegramUserId!.Value)
            .ToListAsync();

        if (!telegramUsers.Any())
        {
            _logger.LogWarning("No Telegram users found for broadcast");
            return 0;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (var userId in telegramUsers)
        {
            try
            {
                // For private chats, chat ID equals user ID
                var success = await SendMessageAsync(userId, message);
                if (success)
                    successCount++;
                else
                    failCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to Telegram user {UserId}", userId);
                failCount++;
            }
        }

        _logger.LogInformation("Broadcast message sent. Success: {SuccessCount}, Failed: {FailCount}", successCount, failCount);
        return successCount;
    }

    /// <summary>
    /// Sends a message to a specific Telegram user by chat ID
    /// </summary>
    public async Task<bool> SendMessageAsync(long chatId, string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_botToken))
            {
                _logger.LogError("Cannot send message: Bot token is empty");
                return false;
            }

            var url = $"{_botApiUrl}/sendMessage";
            _logger.LogDebug("Sending message to Telegram API: {Url} (chatId: {ChatId})", 
                url.Replace(_botToken, "***"), chatId);
            
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "HTML"
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Message sent successfully to chat {ChatId}", chatId);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to send message to chat {ChatId}. Status: {Status}, URL: {Url}, Error: {Error}", 
                    chatId, response.StatusCode, url.Replace(_botToken, "***"), errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending message to chat {ChatId}", chatId);
            return false;
        }
    }
}

