namespace Bebochka.Api.Models.DTOs;

public class ProductKitPartInputDto
{
    /// <summary>Существующий id части (при обновлении комплекта).</summary>
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductKitPartDto
{
    public int ProductId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
    public bool IsReservedByOthers { get; set; }
    public bool InMyCart { get; set; }
    public int QuantityInMyCart { get; set; }
}

public class ProductKitOptionsDto
{
    public int KitId { get; set; }
    public int DisplayProductId { get; set; }
    public decimal KitPrice { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int PartCount { get; set; }
    /// <summary>Хотя бы одна часть занята в чужой корзине — для подписи «бронь комплект».</summary>
    public bool HasKitReservation { get; set; }
    /// <summary>Все части свободны для покупки комплектом целиком.</summary>
    public bool CanAddFullKit { get; set; }
    public List<ProductKitPartDto> Parts { get; set; } = new();
}
