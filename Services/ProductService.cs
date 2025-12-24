using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Helpers;

namespace Bebochka.Api.Services;

/// <summary>
/// Service implementation for product operations
/// </summary>
public class ProductService : IProductService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the ProductService class
    /// </summary>
    /// <param name="context">Database context</param>
    public ProductService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all products from the database, ordered by creation date (newest first)
    /// PublishedAt is stored as Moscow time, so we compare with current Moscow time
    /// </summary>
    /// <param name="sessionId">Optional session ID to exclude from reserved quantity calculation</param>
    /// <returns>List of all products</returns>
    public async Task<List<ProductDto>> GetAllProductsAsync(string? sessionId = null)
    {
        var moscowNow = DateTimeHelper.GetMoscowTime();
        var products = await _context.Products
            .Where(p => p.PublishedAt == null || p.PublishedAt <= moscowNow) // Only show published products (compare with Moscow time)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        // Вычисляем зарезервированное количество для каждого товара
        var productIds = products.Select(p => p.Id).ToList();
        var expirationTime = DateTime.UtcNow.AddMinutes(-20);
        var reservedItems = await _context.CartItems
            .Where(c => productIds.Contains(c.ProductId) && 
                       (sessionId == null || c.SessionId != sessionId) &&
                       c.UpdatedAt > expirationTime) // Только активные резервы (не старше 20 минут)
            .GroupBy(c => c.ProductId)
            .Select(g => new { ProductId = g.Key, Reserved = g.Sum(c => c.Quantity) })
            .ToListAsync();
        
        var reservedQuantities = reservedItems.ToDictionary(x => x.ProductId, x => x.Reserved);

        return products.Select(p => 
        {
            var dto = MapToDto(p);
            // Вычисляем доступное количество (общее - зарезервированное)
            var reserved = reservedQuantities.GetValueOrDefault(p.Id, 0);
            dto.AvailableQuantity = Math.Max(0, p.QuantityInStock - reserved);
            return dto;
        }).ToList();
    }

    /// <summary>
    /// Gets a product by its unique identifier
    /// PublishedAt is stored as Moscow time, so we compare with current Moscow time
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <param name="sessionId">Optional session ID to exclude from reserved quantity calculation</param>
    /// <returns>Product information or null if not found</returns>
    public async Task<ProductDto?> GetProductByIdAsync(int id, string? sessionId = null)
    {
        var moscowNow = DateTimeHelper.GetMoscowTime();
        var product = await _context.Products
            .Where(p => p.Id == id && (p.PublishedAt == null || p.PublishedAt <= moscowNow))
            .FirstOrDefaultAsync();
        if (product == null) return null;

        var dto = MapToDto(product);

        // Вычисляем зарезервированное количество для этого товара
        var expirationTime = DateTime.UtcNow.AddMinutes(-20);
        var reservedQuantity = await _context.CartItems
            .Where(c => c.ProductId == id &&
                       (sessionId == null || c.SessionId != sessionId) &&
                       c.UpdatedAt > expirationTime) // Только активные резервы (не старше 20 минут)
            .SumAsync(c => (int?)c.Quantity) ?? 0;

        // Вычисляем доступное количество (общее - зарезервированное)
        dto.AvailableQuantity = Math.Max(0, product.QuantityInStock - reservedQuantity);

        return dto;
    }

    /// <summary>
    /// Creates a new product in the database
    /// PublishedAt is stored as Moscow time (without UTC conversion)
    /// </summary>
    /// <param name="dto">Product data transfer object</param>
    /// <param name="imagePaths">List of image file paths</param>
    /// <returns>Created product</returns>
    public async Task<ProductDto> CreateProductAsync(CreateProductDto dto, List<string> imagePaths)
    {
        // PublishedAt from frontend is already in Moscow time format, store it directly
        var product = new Product
        {
            Name = dto.Name,
            Brand = dto.Brand,
            Description = dto.Description,
            Price = dto.Price,
            Size = dto.Size,
            Color = dto.Color,
            Images = imagePaths,
            QuantityInStock = dto.QuantityInStock > 0 ? dto.QuantityInStock : 1,
            Gender = dto.Gender,
            Condition = dto.Condition,
            PublishedAt = dto.PublishedAt, // Store as Moscow time directly
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return MapToDto(product);
    }

    /// <summary>
    /// Updates an existing product in the database
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <param name="dto">Updated product data</param>
    /// <param name="imagePaths">List of image file paths</param>
    /// <returns>Updated product or null if not found</returns>
    public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto, List<string> imagePaths)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return null;

        product.Name = dto.Name;
        product.Brand = dto.Brand;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.Size = dto.Size;
        product.Color = dto.Color;
        product.Images = imagePaths;
        product.QuantityInStock = dto.QuantityInStock > 0 ? dto.QuantityInStock : product.QuantityInStock;
        product.Gender = dto.Gender;
        product.Condition = dto.Condition;
        // PublishedAt comes as UTC DateTime but represents Moscow time components
        // Extract components and store as Moscow time
        if (dto.PublishedAt.HasValue)
        {
            product.PublishedAt = new DateTime(
                dto.PublishedAt.Value.Year,
                dto.PublishedAt.Value.Month,
                dto.PublishedAt.Value.Day,
                dto.PublishedAt.Value.Hour,
                dto.PublishedAt.Value.Minute,
                dto.PublishedAt.Value.Second,
                DateTimeKind.Unspecified
            );
        }
        else
        {
            product.PublishedAt = null;
        }
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(product);
    }

    /// <summary>
    /// Deletes a product from the database
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <returns>True if product was deleted, false if not found</returns>
    public async Task<bool> DeleteProductAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Maps a Product entity to a ProductDto
    /// </summary>
    /// <param name="product">Product entity</param>
    /// <returns>Product data transfer object</returns>
    private static ProductDto MapToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Brand = product.Brand,
            Description = product.Description,
            Price = product.Price,
            Size = product.Size,
            Color = product.Color,
            Images = product.Images,
            QuantityInStock = product.QuantityInStock,
            Gender = product.Gender,
            Condition = product.Condition,
            PublishedAt = product.PublishedAt,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
    
    /// <summary>
    /// Gets all products that should be published now but haven't been notified yet
    /// PublishedAt is stored as Moscow time in the database, so we compare with current Moscow time
    /// </summary>
    /// <returns>List of products ready for publication</returns>
    public async Task<List<Product>> GetProductsReadyForPublicationAsync()
    {
        // Get current Moscow time for comparison
        var moscowNow = DateTimeHelper.GetMoscowTime();
        // Check for products published in the last 5 minutes to catch products that just became available
        var fiveMinutesAgo = moscowNow.AddMinutes(-5);
        
        // PublishedAt is stored as Moscow time, so we compare directly
        var products = await _context.Products
            .Where(p => p.PublishedAt != null && 
                       p.PublishedAt <= moscowNow && 
                       p.PublishedAt > fiveMinutesAgo)
            .OrderBy(p => p.PublishedAt)
            .ToListAsync();
        
        return products;
    }
    
    /// <summary>
    /// Gets all products for admin panel (including unpublished products)
    /// </summary>
    /// <returns>List of all products regardless of publication status</returns>
    public async Task<List<ProductDto>> GetAllProductsForAdminAsync()
    {
        var products = await _context.Products
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return products.Select(p => MapToDto(p)).ToList();
    }
    
    /// <summary>
    /// Gets all unpublished products (for announcement selection)
    /// PublishedAt is stored as Moscow time, so we compare with current Moscow time
    /// </summary>
    /// <returns>List of unpublished products</returns>
    public async Task<List<ProductDto>> GetUnpublishedProductsAsync()
    {
        var moscowNow = DateTimeHelper.GetMoscowTime();
        var products = await _context.Products
            .Where(p => p.PublishedAt != null && p.PublishedAt > moscowNow) // Only unpublished products (compare with Moscow time)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return products.Select(p => MapToDto(p)).ToList();
    }
}
