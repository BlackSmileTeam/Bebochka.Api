using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

/// <summary>
/// Service interface for product operations
/// </summary>
public interface IProductService
{
    /// <summary>
    /// Gets all products from the database
    /// </summary>
    /// <param name="sessionId">Optional session ID to exclude from reserved quantity calculation</param>
    /// <returns>List of all products</returns>
    Task<List<ProductDto>> GetAllProductsAsync(string? sessionId = null);

    /// <summary>
    /// Gets a product by its unique identifier
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <param name="sessionId">Optional session ID to exclude from reserved quantity calculation</param>
    /// <returns>Product information or null if not found</returns>
    Task<ProductDto?> GetProductByIdAsync(int id, string? sessionId = null);

    /// <summary>
    /// Creates a new product
    /// </summary>
    /// <param name="dto">Product data transfer object</param>
    /// <param name="imagePaths">List of image file paths</param>
    /// <returns>Created product</returns>
    Task<ProductDto> CreateProductAsync(CreateProductDto dto, List<string> imagePaths);

    /// <summary>
    /// Updates an existing product
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <param name="dto">Updated product data</param>
    /// <param name="imagePaths">List of image file paths</param>
    /// <returns>Updated product or null if not found</returns>
    Task<ProductDto?> UpdateProductAsync(int id, UpdateProductDto dto, List<string> imagePaths);

    /// <summary>
    /// Deletes a product by its identifier
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <returns>True if product was deleted, false if not found</returns>
    Task<bool> DeleteProductAsync(int id);
    
    /// <summary>
    /// Gets all products that should be published now but haven't been notified yet
    /// </summary>
    /// <returns>List of products ready for publication</returns>
    Task<List<Models.Product>> GetProductsReadyForPublicationAsync();
    
    /// <summary>
    /// Gets all products for admin panel (including unpublished products)
    /// </summary>
    /// <returns>List of all products regardless of publication status</returns>
    Task<List<ProductDto>> GetAllProductsForAdminAsync();
    
    /// <summary>
    /// Gets all unpublished products (for announcement selection)
    /// </summary>
    /// <returns>List of unpublished products</returns>
    Task<List<ProductDto>> GetUnpublishedProductsAsync();
}
