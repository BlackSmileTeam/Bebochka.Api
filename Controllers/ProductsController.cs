using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Data;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Bebochka.Api.Helpers;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for managing products in the Bebochka store
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IWebHostEnvironment _environment;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the ProductsController class
    /// </summary>
    public ProductsController(
        IProductService productService,
        IWebHostEnvironment environment,
        IServiceScopeFactory scopeFactory,
        AppDbContext context)
    {
        _productService = productService;
        _environment = environment;
        _scopeFactory = scopeFactory;
        _context = context;
    }

    /// <summary>
    /// Gets all products from the store
    /// </summary>
    /// <returns>List of all products</returns>
    /// <response code="200">Returns the list of products</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductDto>>> GetAllProducts([FromQuery] string? sessionId = null)
    {
        var products = await _productService.GetAllProductsAsync(sessionId);
        return Ok(products);
    }

    /// <summary>
    /// Gets a product by its unique identifier
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <param name="sessionId">Optional session ID to exclude from reserved quantity calculation</param>
    /// <returns>Product information</returns>
    /// <response code="200">Returns the requested product</response>
    /// <response code="404">Product not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProduct(int id, [FromQuery] string? sessionId = null)
    {
        var product = await _productService.GetProductByIdAsync(id, sessionId);
        if (product == null)
            return NotFound();

        return Ok(product);
    }

    /// <summary>
    /// Creates a new product with images
    /// </summary>
    /// <param name="dto">Product data transfer object</param>
    /// <param name="images">Product images (multipart/form-data)</param>
    /// <returns>Created product</returns>
    /// <response code="201">Product created successfully</response>
    /// <response code="400">Invalid input data</response>
    /// <response code="401">Unauthorized</response>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST /api/products
    ///     Content-Type: multipart/form-data
    ///     
    ///     Name: "Детская куртка"
    ///     Brand: "Zara"
    ///     Description: "Описание товара"
    ///     Price: 1500
    ///     Size: "104"
    ///     Color: "Синий"
    ///     Images: [file1.jpg, file2.jpg]
    /// </remarks>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductDto dto)
    {
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (dto == null || string.IsNullOrEmpty(dto.Name))
            {
                return BadRequest(new { message = "Product name is required" });
            }

            var imagePaths = new List<string>();
            var contentStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Обрабатываем base64 изображения
            if (dto.Images != null && dto.Images.Any())
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Uploads folder path: {uploadsFolder}");
                
                try
                {
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Creating uploads directory...");
                        Directory.CreateDirectory(uploadsFolder);
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Directory created successfully");
                    }
                    
                    // Проверяем права на запись
                    var testFile = Path.Combine(uploadsFolder, ".write-test");
                    try
                    {
                        await System.IO.File.WriteAllTextAsync(testFile, "test");
                        System.IO.File.Delete(testFile);
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Write permissions OK");
                    }
                    catch (Exception writeEx)
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] ERROR: Cannot write to uploads folder: {writeEx.Message}");
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Current user: {Environment.UserName}");
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Directory permissions check failed");
                        throw new UnauthorizedAccessException($"Cannot write to uploads folder: {writeEx.Message}");
                    }
                }
                catch (Exception dirEx)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] ERROR creating/accessing uploads folder: {dirEx.Message}");
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Exception type: {dirEx.GetType().Name}");
                    throw;
                }
                
                foreach (var base64Image in dto.Images)
                {
                    if (string.IsNullOrEmpty(base64Image)) continue;
                    
                    try
                    {
                        // Убираем префикс data:image/...;base64, если есть
                        var base64Data = base64Image.Contains(",") 
                            ? base64Image.Split(',')[1] 
                            : base64Image;
                        
                        var imageBytes = Convert.FromBase64String(base64Data);
                        
                        // Определяем расширение по содержимому
                        var extension = ".jpg";
                        if (imageBytes.Length > 2)
                        {
                            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50) extension = ".png";
                            else if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49) extension = ".gif";
                            else if (imageBytes[0] == 0x52 && imageBytes[1] == 0x49) extension = ".webp";
                        }
                        
                        var fileName = $"{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        
                        await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                        imagePaths.Add($"/uploads/{fileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Error processing image: {ex.Message}");
                        // Пропускаем некорректное изображение
                    }
                }
            }

            contentStopwatch.Stop();
            var dbStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var product = await _productService.CreateProductAsync(dto, imagePaths);
            dbStopwatch.Stop();

            totalStopwatch.Stop();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] CreateProduct: загрузка/обработка контента (декодирование и запись изображений) = {contentStopwatch.ElapsedMilliseconds} мс, сохранение в БД = {dbStopwatch.ElapsedMilliseconds} мс, всего в действии = {totalStopwatch.ElapsedMilliseconds} мс ({totalStopwatch.Elapsed.TotalSeconds:F2} с)");

            // Ответ сразу после записи в БД. Пользователь получает ответ и может готовить следующую карточку.
            // Загрузка фото в кэш Telegram и отправка в канал — в фоне, в отдельном scope (не используем scoped-сервисы запроса).
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var scopeFactory = _scopeFactory;
            var productId = product.Id;
            var hasImages = product.Images != null && product.Images.Count > 0;
            var publishToChannel = dto.PublishedAt.HasValue;
            string? caption = null;
            List<string>? imageUrls = null;
            if (publishToChannel)
            {
                caption = $"🛍️ {product.Name}\n";
                if (!string.IsNullOrEmpty(product.Brand)) caption += $"🏷️ Бренд: {product.Brand}\n";
                if (!string.IsNullOrEmpty(product.Size)) caption += $"📏 Размер: {product.Size}\n";
                if (!string.IsNullOrEmpty(product.Color)) caption += $"🎨 Цвет: {product.Color}\n";
                if (!string.IsNullOrEmpty(product.Gender)) caption += $"👤 Пол: {product.Gender}\n";
                if (!string.IsNullOrEmpty(product.Condition)) caption += $"✨ Состояние: {product.Condition}\n";
                if (!string.IsNullOrEmpty(product.Description)) caption += $"\n📝 {product.Description}\n";
                caption += $"\n💰 Цена: {product.Price:N0} ₽\n";
                if (product.Images != null && product.Images.Any())
                {
                    imageUrls = new List<string>();
                    foreach (var imagePath in product.Images)
                    {
                        if (string.IsNullOrEmpty(imagePath)) continue;
                        if (imagePath.StartsWith("http")) imageUrls.Add(imagePath);
                        else if (imagePath.StartsWith("/")) imageUrls.Add($"{baseUrl}{imagePath}");
                        else imageUrls.Add($"{baseUrl}/{imagePath.TrimStart('/')}");
                    }
                }
            }

            // Resolve current user's preferred channel emoji (Telegram custom_emoji_id)
            string? channelEmojiId = null;
            var userIdClaim = User.FindFirst("UserId")?.Value
                              ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var currentUserId))
            {
                var currentUser = await _context.Users.FindAsync(currentUserId);
                channelEmojiId = currentUser?.ChannelCustomEmojiId;
            }

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramNotificationService>();
                try
                {
                    if (hasImages)
                        await telegramService.PreCacheProductImagesAsync(productId, baseUrl);
                    if (publishToChannel && caption != null)
                    {
                        if (imageUrls != null && imageUrls.Count > 0)
                            await telegramService.SendMessageToChannelWithPhotosAsync(caption, imageUrls, null, channelEmojiId);
                        else
                            await telegramService.SendMessageToChannelAsync(caption, channelEmojiId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Background Telegram error: {ex.Message}");
                }
            });

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] ERROR: {ex.Message}");
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing product
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <param name="dto">Updated product data</param>
    /// <param name="images">New product images (optional)</param>
    /// <param name="existingImages">Existing images to preserve (optional)</param>
    /// <returns>Updated product</returns>
    /// <response code="200">Product updated successfully</response>
    /// <response code="404">Product not found</response>
    /// <response code="401">Unauthorized</response>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductDto>> UpdateProduct(int id, [FromBody] UpdateProductDto dto)
    {
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Get existing product
            var existingProduct = await _productService.GetProductByIdAsync(id);
            if (existingProduct == null)
                return NotFound();

            var imagePaths = new List<string>();
            var contentStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Add existing images that should be preserved (sent from frontend)
            if (dto.ExistingImages != null && dto.ExistingImages.Any())
            {
                imagePaths.AddRange(dto.ExistingImages);
            }

            // Add new uploaded images (base64)
            if (dto.Images != null && dto.Images.Any())
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var base64Image in dto.Images)
                {
                    if (string.IsNullOrEmpty(base64Image)) continue;

                    try
                    {
                        var base64Data = base64Image.Contains(",") 
                            ? base64Image.Split(',')[1] 
                            : base64Image;
                        
                        var imageBytes = Convert.FromBase64String(base64Data);
                        
                        var extension = ".jpg";
                        if (imageBytes.Length > 2)
                        {
                            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                                extension = ".png";
                            else if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
                                extension = ".gif";
                            else if (imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x46)
                                extension = ".webp";
                            else if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                                extension = ".jpeg";
                        }

                        var fileName = $"{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadsFolder, fileName);

                        await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                        imagePaths.Add($"/uploads/{fileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Error processing new image for update: {ex.Message}");
                    }
                }
            }

            contentStopwatch.Stop();
            var dbStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var product = await _productService.UpdateProductAsync(id, dto, imagePaths);
            dbStopwatch.Stop();
            if (product == null)
                return NotFound();

            totalStopwatch.Stop();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] UpdateProduct(id={id}): загрузка/обработка контента = {contentStopwatch.ElapsedMilliseconds} мс, сохранение в БД = {dbStopwatch.ElapsedMilliseconds} мс, всего в действии = {totalStopwatch.ElapsedMilliseconds} мс ({totalStopwatch.Elapsed.TotalSeconds:F2} с)");

            // Предзагрузка фото в кэш Telegram — в фоне, в отдельном scope
            if (product.Images != null && product.Images.Count > 0)
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var productId = product.Id;
                var scopeFactory = _scopeFactory;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramNotificationService>();
                        await telegramService.PreCacheProductImagesAsync(productId, baseUrl);
                    }
                    catch (Exception ex) { Console.WriteLine($"[PreCache] Error: {ex.Message}"); }
                });
            }

            return Ok(product);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] [ERROR] Exception in UpdateProduct: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the product.", details = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a product by its identifier
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <returns>No content</returns>
    /// <response code="204">Product deleted successfully</response>
    /// <response code="404">Product not found</response>
    /// <response code="401">Unauthorized</response>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var result = await _productService.DeleteProductAsync(id);
        if (!result)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Gets all products for admin panel (including unpublished products)
    /// </summary>
    /// <returns>List of all products regardless of publication status</returns>
    /// <response code="200">Returns the list of all products</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet("admin/all")]
    [Authorize]
    [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ProductDto>>> GetAllProductsForAdmin()
    {
        var products = await _productService.GetAllProductsForAdminAsync();
        return Ok(products);
    }

    /// <summary>
    /// Marks a product as published by setting PublishedAt to current time
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <returns>Updated product</returns>
    /// <response code="200">Product published successfully</response>
    /// <response code="404">Product not found</response>
    /// <response code="401">Unauthorized</response>
    [HttpPatch("{id}/publish")]
    [Authorize]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProductDto>> PublishProduct(int id)
    {
        try
        {
            // Get current Moscow time
            var moscowNow = DateTimeHelper.GetMoscowTime();
            
            // Create update DTO with only PublishedAt set
            var updateDto = new UpdateProductDto
            {
                PublishedAt = moscowNow
            };
            
            // Update product with empty image list to preserve existing images (we're only updating PublishedAt)
            var product = await _productService.UpdateProductAsync(id, updateDto, new List<string>());
            if (product == null)
                return NotFound();

            return Ok(product);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] [ERROR] Exception in PublishProduct: {ex.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while publishing the product.", details = ex.Message });
        }
    }
}

