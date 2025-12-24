using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Bebochka.Api.Data;
using System.Net.Http.Headers;

namespace Bebochka.Api.Services;

/// <summary>
/// Service for sending notifications via Telegram Bot API
/// </summary>
public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly string _botToken;
    private readonly string _botApiUrl;

    public TelegramNotificationService(
        HttpClient httpClient,
        ILogger<TelegramNotificationService> logger,
        IConfiguration configuration,
        AppDbContext context,
        IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        _logger = logger;
        _context = context;
        _environment = environment;
        
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

    /// <summary>
    /// Sends a photo with caption to a specific Telegram user by chat ID
    /// </summary>
    public async Task<bool> SendPhotoAsync(long chatId, string photoPath, string? caption = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_botToken))
            {
                _logger.LogError("Cannot send photo: Bot token is empty");
                return false;
            }

            var webRootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPhotoPath = Path.Combine(webRootPath, photoPath.TrimStart('/'));
            
            if (!File.Exists(fullPhotoPath))
            {
                _logger.LogError("Photo file not found: {PhotoPath}", fullPhotoPath);
                return false;
            }

            var url = $"{_botApiUrl}/sendPhoto";
            
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(chatId.ToString()), "chat_id");
            if (!string.IsNullOrWhiteSpace(caption))
            {
                content.Add(new StringContent(caption), "caption");
                content.Add(new StringContent("HTML"), "parse_mode");
            }
            
            var fileBytes = await File.ReadAllBytesAsync(fullPhotoPath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "photo", Path.GetFileName(fullPhotoPath));

            var response = await _httpClient.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Photo sent successfully to chat {ChatId}", chatId);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to send photo to chat {ChatId}. Status: {Status}, Error: {Error}", 
                    chatId, response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending photo to chat {ChatId}", chatId);
            return false;
        }
    }

    /// <summary>
    /// Sends a broadcast message with photos to all Telegram users
    /// </summary>
    public async Task<int> SendBroadcastWithPhotosAsync(string message, List<string> photoPaths)
    {
        if (string.IsNullOrWhiteSpace(message) || photoPaths == null || photoPaths.Count == 0)
        {
            _logger.LogWarning("Attempted to send empty broadcast message or no photos");
            return 0;
        }

        // Get all users who have Telegram User IDs
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
                bool success = true;
                
                // Send message first
                if (!await SendMessageAsync(userId, message))
                {
                    success = false;
                }
                
                // Send photos
                foreach (var photoPath in photoPaths)
                {
                    if (!await SendPhotoAsync(userId, photoPath, null))
                    {
                        success = false;
                    }
                    
                    // Small delay between photos to avoid rate limits
                    await Task.Delay(100);
                }
                
                if (success)
                    successCount++;
                else
                    failCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message with photos to Telegram user {UserId}", userId);
                failCount++;
            }
            
            // Small delay between users to avoid rate limits
            await Task.Delay(50);
        }

        _logger.LogInformation("Broadcast message with photos sent. Success: {SuccessCount}, Failed: {FailCount}", successCount, failCount);
        return successCount;
    }
}

