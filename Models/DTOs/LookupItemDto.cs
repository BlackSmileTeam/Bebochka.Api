namespace Bebochka.Api.Models.DTOs;

public class LookupItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ProductCount { get; set; }
}

public class LookupItemCreateDto
{
    public string Name { get; set; } = string.Empty;
}

public class BulkProductDiscountDto
{
    public List<int> ProductIds { get; set; } = new();
    /// <summary>1–99 to set; null or 0 to clear.</summary>
    public int? DiscountPercent { get; set; }
}
