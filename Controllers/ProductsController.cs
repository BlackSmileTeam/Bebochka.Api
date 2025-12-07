using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;
using Microsoft.AspNetCore.Authorization;

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

    /// <summary>
    /// Initializes a new instance of the ProductsController class
    /// </summary>
    /// <param name="productService">Service for product operations</param>
    /// <param name="environment">Web hosting environment</param>
    public ProductsController(IProductService productService, IWebHostEnvironment environment)
    {
        _productService = productService;
        _environment = environment;
    }

    /// <summary>
    /// Gets all products from the store
    /// </summary>
    /// <returns>List of all products</returns>
    /// <response code="200">Returns the list of products</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProductDto>>> GetAllProducts()
    {
        var products = await _productService.GetAllProductsAsync();
        return Ok(products);
    }

    /// <summary>
    /// Gets a product by its unique identifier
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <returns>Product information</returns>
    /// <response code="200">Returns the requested product</response>
    /// <response code="404">Product not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
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
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)] // 10MB
    [DisableRequestSizeLimit]
    public async Task<ActionResult<ProductDto>> CreateProduct()
    {
        var startTime = DateTime.UtcNow;
        Console.WriteLine($"[{startTime:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] CreateProduct STARTED");
        
        try
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Reading form manually...");
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] HasFormContentType: {Request.HasFormContentType}");
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Body.CanSeek: {Request.Body.CanSeek}");
            
            // Включаем буферизацию, если еще не включена
            if (!Request.Body.CanSeek)
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Enabling buffering...");
                Request.EnableBuffering();
            }
            
            // Читаем форму вручную
            var form = await Request.ReadFormAsync();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Form read successfully. Keys: {string.Join(", ", form.Keys)}");
            
            // Создаем DTO из формы
            var dto = new CreateProductDto
            {
                Name = form["name"].ToString(),
                Brand = form["brand"].ToString(),
                Description = form["description"].ToString(),
                Price = decimal.TryParse(form["price"].ToString(), out var price) ? price : 0,
                Size = form["size"].ToString(),
                Color = form["color"].ToString()
            };
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] DTO created - Name: {dto.Name}");
            
            if (string.IsNullOrEmpty(dto.Name))
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] ERROR: Product name is required");
                return BadRequest(new { message = "Product name is required" });
            }
            
            // Получаем файлы
            var files = form.Files.ToList();
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Total files to process: {files.Count}");
            
            var imagePaths = new List<string>();

            if (files != null && files.Any())
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Processing {files.Count} files...");
                var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Uploads folder: {uploadsFolder}");
                
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Created uploads folder");
                }

                foreach (var image in files)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Processing file: {image.FileName}, Size: {image.Length}");
                    if (image.Length > 0)
                    {
                        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Saving to: {filePath}");

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(stream);
                        }

                        imagePaths.Add($"/uploads/{fileName}");
                        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] File saved: {fileName}");
                    }
                }
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] All files processed. Total: {imagePaths.Count}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] No files to process");
            }

            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Creating product in database...");
            var product = await _productService.CreateProductAsync(dto, imagePaths);
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] Product created with ID: {product.Id}");
            
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [ProductsController] CreateProduct COMPLETED in {duration:F2} ms");
            
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
    public async Task<ActionResult<ProductDto>> UpdateProduct(int id, [FromForm] UpdateProductDto dto, [FromForm] List<IFormFile>? images = null, [FromForm] List<string>? existingImages = null)
    {
        // Get existing product
        var existingProduct = await _productService.GetProductByIdAsync(id);
        if (existingProduct == null)
            return NotFound();

        var imagePaths = new List<string>();

        // Add existing images that should be preserved (sent from frontend)
        if (existingImages != null && existingImages.Any())
        {
            imagePaths.AddRange(existingImages);
        }

        // Add new uploaded images
        if (images != null && images.Any())
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            foreach (var image in images)
            {
                if (image.Length > 0)
                {
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    imagePaths.Add($"/uploads/{fileName}");
                }
            }
        }

        var product = await _productService.UpdateProductAsync(id, dto, imagePaths);
        if (product == null)
            return NotFound();

        return Ok(product);
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
}

