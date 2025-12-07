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
    /// <returns>List of all products</returns>
    Task<List<ProductDto>> GetAllProductsAsync();

    /// <summary>
    /// Gets a product by its unique identifier
    /// </summary>
    /// <param name="id">Product identifier</param>
    /// <returns>Product information or null if not found</returns>
    Task<ProductDto?> GetProductByIdAsync(int id);

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
}
