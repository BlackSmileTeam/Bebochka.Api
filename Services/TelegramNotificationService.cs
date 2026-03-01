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
    private readonly string? _channelId;

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
        _channelId = configuration["TelegramBot:ChannelId"];
        
        // Log token presence (but not the actual token for security)
        _logger.LogInformation("TelegramNotificationService initialized. Bot token configured: {TokenPresent}, Channel ID configured: {ChannelIdPresent}, Channel ID value: {ChannelIdValue}", 
            !string.IsNullOrEmpty(_botToken), !string.IsNullOrEmpty(_channelId), 
            string.IsNullOrEmpty(_channelId) ? "EMPTY" : _channelId);
        
        if (string.IsNullOrWhiteSpace(_channelId))
        {
            _logger.LogWarning("TelegramBot:ChannelId is not configured. Channel messages will fail. Check environment variable TelegramBot__ChannelId or configuration key TelegramBot:ChannelId");
        }
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

    /// <summary>
    /// Sends a message to a Telegram channel
    /// </summary>
    public async Task<bool> SendMessageToChannelAsync(string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_botToken))
            {
                _logger.LogError("Cannot send message to channel: Bot token is empty");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_channelId))
            {
                _logger.LogError("Cannot send message to channel: Channel ID is not configured. Please set TelegramBot__ChannelId environment variable or TelegramBot:ChannelId in appsettings.json");
                return false;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Attempted to send empty message to channel");
                return false;
            }

            var url = $"{_botApiUrl}/sendMessage";
            _logger.LogDebug("Sending message to Telegram channel: {Url} (channelId: {ChannelId})", 
                url.Replace(_botToken, "***"), _channelId);
            
            var payload = new
            {
                chat_id = _channelId,
                text = message,
                parse_mode = "HTML"
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Message sent successfully to channel {ChannelId}", _channelId);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to send message to channel {ChannelId}. Status: {Status}, URL: {Url}, Error: {Error}", 
                    _channelId, response.StatusCode, url.Replace(_botToken, "***"), errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending message to channel {ChannelId}", _channelId);
            return false;
        }
    }

    /// <summary>
    /// Sends a message with photos to a Telegram channel
    /// </summary>
    public async Task<bool> SendMessageToChannelWithPhotosAsync(string message, List<string> imageUrls)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_botToken))
            {
                _logger.LogError("Cannot send message to channel: Bot token is empty");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_channelId))
            {
                _logger.LogError("Cannot send message to channel: Channel ID is not configured. Please set TelegramBot__ChannelId environment variable or TelegramBot:ChannelId in appsettings.json");
                return false;
            }

            if (imageUrls == null || imageUrls.Count == 0)
            {
                // Если нет изображений, отправляем только текст
                return await SendMessageToChannelAsync(message);
            }

            // Скачиваем изображения
            var images = new List<(byte[] Bytes, string Extension)>();
            _logger.LogInformation("Starting to download {Count} images for channel message", imageUrls.Count);
            
            foreach (var imageUrl in imageUrls)
            {
                try
                {
                    _logger.LogDebug("Downloading image from {ImageUrl}", imageUrl);
                    var imageResponse = await _httpClient.GetAsync(imageUrl);
                    
                    if (imageResponse.IsSuccessStatusCode)
                    {
                        var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                        var extension = System.IO.Path.GetExtension(new Uri(imageUrl).AbsolutePath);
                        if (string.IsNullOrEmpty(extension))
                            extension = ".jpg";
                        
                        _logger.LogDebug("Successfully downloaded image: {Size} bytes, extension: {Extension}", 
                            imageBytes.Length, extension);
                        images.Add((imageBytes, extension));
                    }
                    else
                    {
                        _logger.LogWarning("Failed to download image from {ImageUrl}. Status: {Status}", 
                            imageUrl, imageResponse.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Exception while downloading image from {ImageUrl}", imageUrl);
                }
            }
            
            _logger.LogInformation("Downloaded {Count} images out of {Total}", images.Count, imageUrls.Count);

            if (images.Count == 0)
            {
                // Если не удалось скачать изображения, отправляем только текст
                return await SendMessageToChannelAsync(message);
            }

            // Если одно изображение, отправляем через sendPhoto
            if (images.Count == 1)
            {
                var url = $"{_botApiUrl}/sendPhoto";
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(_channelId), "chat_id");
                content.Add(new ByteArrayContent(images[0].Bytes), "photo", $"photo{images[0].Extension}");
                if (!string.IsNullOrWhiteSpace(message))
                {
                    content.Add(new StringContent(message), "caption");
                    content.Add(new StringContent("HTML"), "parse_mode");
                }

                var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }

            // Если несколько изображений, отправляем через sendMediaGroup
            var mediaGroupUrl = $"{_botApiUrl}/sendMediaGroup";
            using var mediaContent = new MultipartFormDataContent();
            mediaContent.Add(new StringContent(_channelId), "chat_id");

            // Формируем массив media объектов для JSON
            var mediaArray = new List<Dictionary<string, object>>();
            for (int i = 0; i < images.Count; i++)
            {
                var mediaObj = new Dictionary<string, object>
                {
                    { "type", "photo" },
                    { "media", $"attach://photo_{i}" }
                };
                
                // Добавляем caption только к последнему фото
                if (i == images.Count - 1 && !string.IsNullOrWhiteSpace(message))
                {
                    mediaObj["caption"] = message;
                    mediaObj["parse_mode"] = "HTML";
                }
                
                mediaArray.Add(mediaObj);
            }

            // Добавляем media как JSON массив строкой
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false
            };
            var mediaJson = System.Text.Json.JsonSerializer.Serialize(mediaArray, options);
            mediaContent.Add(new StringContent(mediaJson), "media");

            // Добавляем файлы с правильными именами
            for (int i = 0; i < images.Count; i++)
            {
                var fileContent = new ByteArrayContent(images[i].Bytes);
                // Определяем правильный Content-Type по расширению
                var contentType = images[i].Extension.ToLower() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                // Важно: имя файла должно совпадать с attach://photo_{i}
                mediaContent.Add(fileContent, $"photo_{i}", $"photo_{i}{images[i].Extension}");
            }

            _logger.LogDebug("Sending media group with {Count} photos to channel {ChannelId}", images.Count, _channelId);
            var mediaResponse = await _httpClient.PostAsync(mediaGroupUrl, mediaContent);
            
            if (mediaResponse.IsSuccessStatusCode)
            {
                _logger.LogDebug("Media group sent successfully to channel {ChannelId}", _channelId);
                return true;
            }
            else
            {
                var errorContent = await mediaResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to send media group to channel {ChannelId}. Status: {Status}, Error: {Error}", 
                    _channelId, mediaResponse.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending message with photos to channel {ChannelId}", _channelId);
            // В случае ошибки пытаемся отправить только текст
            return await SendMessageToChannelAsync(message);
        }
    }
}

