using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Services;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Request DTO for sending products to channel
/// </summary>
public class SendProductsToChannelRequestDto
{
    /// <summary>
    /// Gets or sets the list of product IDs to send
    /// </summary>
    public List<int> ProductIds { get; set; } = new();
}

/// <summary>
/// Result for a single product send operation
/// </summary>
public class ProductSendResult
{
    /// <summary>
    /// Gets or sets the product ID
    /// </summary>
    public int ProductId { get; set; }
    
    /// <summary>
    /// Gets or sets the product name
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets whether the send was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets error message if send failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response DTO for sending products to channel
/// </summary>
public class SendProductsToChannelResponseDto
{
    /// <summary>
    /// Gets or sets whether all products were sent successfully
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the number of successfully sent products
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of failed products
    /// </summary>
    public int FailCount { get; set; }
    
    /// <summary>
    /// Gets or sets the total number of products
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Gets or sets the results for each product
    /// </summary>
    public List<ProductSendResult> Results { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the response message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for checking send status
/// </summary>
public class SendStatusResponseDto
{
    /// <summary>
    /// Gets or sets the total number of products
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of published products
    /// </summary>
    public int PublishedCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of remaining products to send
    /// </summary>
    public int RemainingCount { get; set; }
}

/// <summary>
/// Controller for sending messages via Telegram bot
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TelegramController : ControllerBase
{
    private readonly ITelegramNotificationService _telegramService;
    private readonly ILogger<TelegramController> _logger;
    private readonly AppDbContext _context;
    private readonly IProductService _productService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public TelegramController(
        ITelegramNotificationService telegramService,
        ILogger<TelegramController> logger,
        AppDbContext context,
        IProductService productService,
        IServiceScopeFactory serviceScopeFactory)
    {
        _telegramService = telegramService;
        _logger = logger;
        _context = context;
        _productService = productService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Registers a Telegram User ID for notifications (called automatically when user interacts with bot)
    /// </summary>
    /// <param name="telegramUserId">Telegram User ID</param>
    /// <returns>Success response</returns>
    /// <response code="200">Telegram User ID registered successfully</response>
    [HttpPost("register/{telegramUserId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RegisterTelegramUser(long telegramUserId)
    {
        try
        {
            // Check if user with this Telegram ID already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId);

            if (existingUser != null)
            {
                // User already registered, ensure they are active
                if (!existingUser.IsActive)
                {
                    existingUser.IsActive = true;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Reactivated user with Telegram ID {TelegramUserId}", telegramUserId);
                }
                return Ok(new { message = "Telegram User ID already registered", registered = true });
            }

            // Create new user entry for notifications only
            // If no user with this Telegram ID exists, create a minimal user entry
            // Generate unique username by appending timestamp to avoid conflicts
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var username = $"telegram_{telegramUserId}_{timestamp}";
            
            // Try to create user, but handle potential username conflicts
            var maxAttempts = 3;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    var newUser = new User
                    {
                        Username = attempt == 0 ? username : $"{username}_{attempt}",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // Random password, won't be used
                        TelegramUserId = telegramUserId,
                        IsActive = true,
                        IsAdmin = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Registered new Telegram user {TelegramUserId}", telegramUserId);
                    return Ok(new { message = "Telegram User ID registered successfully", registered = true });
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("Duplicate") == true || 
                                                    ex.InnerException?.Message?.Contains("unique") == true)
                {
                    // Username conflict, try again with different username
                    if (attempt == maxAttempts - 1)
                    {
                        _logger.LogError(ex, "Failed to create user after {Attempts} attempts", maxAttempts);
                        throw;
                    }
                    continue;
                }
            }

            return StatusCode(500, new { message = "Failed to register Telegram user after multiple attempts" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering Telegram user {TelegramUserId}", telegramUserId);
            // If error is due to duplicate username, try to find and update existing user
            if (ex.Message.Contains("Duplicate") || ex.Message.Contains("unique"))
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == $"telegram_{telegramUserId}");
                if (existingUser != null)
                {
                    existingUser.TelegramUserId = telegramUserId;
                    existingUser.IsActive = true;
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Telegram User ID registered successfully", registered = true });
                }
            }
            return StatusCode(500, new { message = "Error registering Telegram user", error = ex.Message });
        }
    }

    /// <summary>
    /// Sends a broadcast message to all Telegram bot users
    /// </summary>
    /// <param name="request">Message request</param>
    /// <returns>Result with number of users who received the message</returns>
    /// <response code="200">Message sent successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("broadcast")]
    [Authorize]
    [ProducesResponseType(typeof(BroadcastMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BroadcastMessageResponseDto>> SendBroadcast([FromBody] BroadcastMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message text is required" });
        }

        try
        {
            var sentCount = await _telegramService.SendBroadcastMessageAsync(request.Message);
            
            return Ok(new BroadcastMessageResponseDto
            {
                Success = true,
                SentCount = sentCount,
                Message = "Broadcast message sent successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending broadcast message");
            return StatusCode(500, new { message = "Error sending broadcast message", error = ex.Message });
        }
    }

    /// <summary>
    /// Sends a message to a specific Telegram user
    /// </summary>
    /// <param name="chatId">Telegram chat ID</param>
    /// <param name="request">Message request</param>
    /// <returns>Result indicating success or failure</returns>
    /// <response code="200">Message sent successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("send/{chatId}")]
    [Authorize]
    [ProducesResponseType(typeof(SendMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendMessageResponseDto>> SendMessage(long chatId, [FromBody] SendMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message text is required" });
        }

        try
        {
            var success = await _telegramService.SendMessageAsync(chatId, request.Message);
            
            return Ok(new SendMessageResponseDto
            {
                Success = success,
                Message = success ? "Message sent successfully" : "Failed to send message"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to chat {ChatId}", chatId);
            return StatusCode(500, new { message = "Error sending message", error = ex.Message });
        }
    }

    /// <summary>
    /// Sends a message to the Telegram channel
    /// </summary>
    /// <param name="request">Message request</param>
    /// <returns>Result indicating success or failure</returns>
    /// <response code="200">Message sent successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("channel/send")]
    [Authorize]
    [ProducesResponseType(typeof(SendMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendMessageResponseDto>> SendMessageToChannel([FromBody] SendMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message text is required" });
        }

        try
        {
            bool success;
            if (request.ImageUrls != null && request.ImageUrls.Count > 0)
            {
                success = await _telegramService.SendMessageToChannelWithPhotosAsync(request.Message, request.ImageUrls);
            }
            else
            {
                success = await _telegramService.SendMessageToChannelAsync(request.Message);
            }
            
            return Ok(new SendMessageResponseDto
            {
                Success = success,
                Message = success ? "Message sent successfully to channel" : "Failed to send message to channel"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to channel");
            return StatusCode(500, new { message = "Error sending message to channel", error = ex.Message });
        }
    }

    /// <summary>
    /// Sends products to Telegram channel by product IDs
    /// All message formatting and image loading happens on the backend
    /// </summary>
    /// <param name="request">Request containing product IDs</param>
    /// <returns>Response with success status and details</returns>
    /// <response code="200">Products sent successfully (or partially)</response>
    /// <response code="400">Invalid request (no product IDs provided)</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("channel/send-products")]
    [Authorize]
    [ProducesResponseType(typeof(SendProductsToChannelResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendProductsToChannelResponseDto>> SendProductsToChannel([FromBody] SendProductsToChannelRequestDto request)
    {
        if (request.ProductIds == null || request.ProductIds.Count == 0)
        {
            return BadRequest(new { message = "Product IDs are required" });
        }

        try
        {
            var results = new List<ProductSendResult>();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            // Load products from database
            var products = await _context.Products
                .Where(p => request.ProductIds.Contains(p.Id))
                .ToListAsync();

            if (products.Count == 0)
            {
                return BadRequest(new { message = "No products found with provided IDs" });
            }

            // Send all products to channel asynchronously (in parallel)
            // Update PublishedAt immediately after each successful send for real-time progress tracking
            var moscowNow = Bebochka.Api.Helpers.DateTimeHelper.GetMoscowTime();
            var sendTasks = products.Select(async product =>
            {
                try
                {
                    // Format message on backend
                    var caption = $"🛍️ {product.Name}\n";
                    if (!string.IsNullOrEmpty(product.Brand))
                        caption += $"🏷️ Бренд: {product.Brand}\n";
                    if (!string.IsNullOrEmpty(product.Size))
                        caption += $"📏 Размер: {product.Size}\n";
                    if (!string.IsNullOrEmpty(product.Color))
                        caption += $"🎨 Цвет: {product.Color}\n";
                    if (!string.IsNullOrEmpty(product.Gender))
                        caption += $"👤 Пол: {product.Gender}\n";
                    if (!string.IsNullOrEmpty(product.Condition))
                        caption += $"✨ Состояние: {product.Condition}\n";
                    if (!string.IsNullOrEmpty(product.Description))
                        caption += $"\n📝 {product.Description}\n";
                    caption += $"\n💰 Цена: {product.Price:N0} ₽\n";

                    // Build image URLs from database paths
                    var imageUrls = new List<string>();
                    if (product.Images != null && product.Images.Any())
                    {
                        foreach (var imagePath in product.Images)
                        {
                            if (string.IsNullOrEmpty(imagePath)) continue;

                            string fullUrl;
                            if (imagePath.StartsWith("http"))
                            {
                                fullUrl = imagePath;
                            }
                            else if (imagePath.StartsWith("/"))
                            {
                                fullUrl = $"{baseUrl}{imagePath}";
                            }
                            else
                            {
                                fullUrl = $"{baseUrl}/{imagePath.TrimStart('/')}";
                            }
                            imageUrls.Add(fullUrl);
                        }
                    }

                    // Send to channel
                    bool success;
                    if (imageUrls.Any())
                    {
                        success = await _telegramService.SendMessageToChannelWithPhotosAsync(caption, imageUrls);
                    }
                    else
                    {
                        success = await _telegramService.SendMessageToChannelAsync(caption);
                    }

                    // Update PublishedAt immediately after successful send for real-time progress tracking
                    if (success)
                    {
                        // Use a separate scope to avoid concurrency issues with parallel updates
                        using var scope = _serviceScopeFactory.CreateScope();
                        var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var productToUpdate = await scopedContext.Products.FindAsync(product.Id);
                        if (productToUpdate != null)
                        {
                            productToUpdate.PublishedAt = moscowNow;
                            productToUpdate.UpdatedAt = DateTime.UtcNow;
                            await scopedContext.SaveChangesAsync();
                            _logger.LogInformation("Product {ProductId} ({ProductName}) successfully sent and PublishedAt updated", 
                                product.Id, product.Name);
                        }
                    }

                    return new ProductSendResult
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Success = success
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending product {ProductId} ({ProductName}) to channel. Exception type: {ExceptionType}, Message: {Message}", 
                        product.Id, product.Name, ex.GetType().Name, ex.Message);
                    
                    // Provide more detailed error message
                    string errorMessage = ex.Message;
                    if (ex is TaskCanceledException || (ex.InnerException != null && ex.InnerException is TimeoutException))
                    {
                        errorMessage = "Timeout while sending to Telegram channel. The images may be too large or the connection is slow.";
                    }
                    else if (ex.Message.Contains("Broken pipe") || ex.Message.Contains("Unable to write data"))
                    {
                        errorMessage = "Connection was interrupted while sending. This may happen with large images. Please try again.";
                    }
                    
                    return new ProductSendResult
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Success = false,
                        ErrorMessage = errorMessage
                    };
                }
            });

            // Wait for all tasks to complete (all products sent in parallel)
            var taskResults = await Task.WhenAll(sendTasks);
            results.AddRange(taskResults);

            var successCount = results.Count(r => r.Success);
            var failCount = results.Count(r => !r.Success);

            return Ok(new SendProductsToChannelResponseDto
            {
                Success = failCount == 0,
                SuccessCount = successCount,
                FailCount = failCount,
                TotalCount = results.Count,
                Results = results,
                Message = failCount == 0
                    ? $"All {successCount} product(s) sent successfully"
                    : $"Sent {successCount} product(s), {failCount} failed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending products to channel");
            return StatusCode(500, new { message = "Error sending products to channel", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets the sending status for products by their IDs
    /// Returns how many products have been published (sent to channel)
    /// </summary>
    /// <param name="productIds">List of product IDs to check</param>
    /// <returns>Status with count of published products</returns>
    /// <response code="200">Status retrieved successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("channel/send-status")]
    [Authorize]
    [ProducesResponseType(typeof(SendStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendStatusResponseDto>> GetSendStatus([FromBody] List<int> productIds)
    {
        if (productIds == null || productIds.Count == 0)
        {
            return BadRequest(new { message = "Product IDs are required" });
        }

        try
        {
            // Count how many products have been published (have PublishedAt set)
            var publishedCount = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.PublishedAt != null)
                .CountAsync();

            return Ok(new SendStatusResponseDto
            {
                TotalCount = productIds.Count,
                PublishedCount = publishedCount,
                RemainingCount = productIds.Count - publishedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting send status for products");
            return StatusCode(500, new { message = "Error getting send status", error = ex.Message });
        }
    }
}

/// <summary>
/// Request DTO for broadcast message
/// </summary>
public class BroadcastMessageRequestDto
{
    /// <summary>
    /// Gets or sets the message text to send
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for broadcast message
/// </summary>
public class BroadcastMessageResponseDto
{
    /// <summary>
    /// Gets or sets whether the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the number of users who received the message
    /// </summary>
    public int SentCount { get; set; }
    
    /// <summary>
    /// Gets or sets the response message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for sending message to specific user
/// </summary>
public class SendMessageRequestDto
{
    /// <summary>
    /// Gets or sets the message text to send
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the list of image URLs to send with the message
    /// </summary>
    public List<string>? ImageUrls { get; set; }
}

/// <summary>
/// Response DTO for sending message
/// </summary>
public class SendMessageResponseDto
{
    /// <summary>
    /// Gets or sets whether the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the response message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
