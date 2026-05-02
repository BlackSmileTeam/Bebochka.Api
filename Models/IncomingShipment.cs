namespace Bebochka.Api.Models;

/// <summary>
/// Входящая поставка (закупка).
/// </summary>
public class IncomingShipment
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal WeightKg { get; set; }

    public int ItemCount { get; set; }

    public decimal OrderedAmount { get; set; }

    public decimal? Profit { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
