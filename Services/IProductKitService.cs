using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

public interface IProductKitService
{
    Task<ProductDto> CreateKitAsync(CreateProductDto dto, List<string> imagePaths);
    Task<ProductDto?> UpdateKitAsync(int displayProductId, UpdateProductDto dto, List<string> imagePaths);
    Task<bool> DeleteKitByProductIdAsync(int productId);
    Task<ProductKitOptionsDto?> GetKitOptionsAsync(int productId, string? sessionId, int? currentUserId);
    Task EnrichProductDtoAsync(ProductDto dto, string? sessionId, int? currentUserId);
    Task<List<int>> GetKitProductIdsAsync(int kitId);
    /// <summary>Если все части комплекта уже в корзине по одной — переводит их в bundle.</summary>
    Task<Models.CartItem?> TryPromoteKitPartsToBundleAsync(int kitId, string? sessionId, int? userId);
}
