namespace Bebochka.Api.Models;

/// <summary>
/// Комплект одежды: общая цена за комплект и связанные товары (части).
/// </summary>
public class ProductKit
{
    public int Id { get; set; }

    /// <summary>Цена при покупке всего комплекта.</summary>
    public decimal KitPrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
