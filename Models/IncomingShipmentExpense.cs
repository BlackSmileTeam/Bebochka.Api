namespace Bebochka.Api.Models;

/// <summary>
/// Статья расхода по входящей поставке.
/// </summary>
public class IncomingShipmentExpense
{
    public int Id { get; set; }

    public int IncomingShipmentId { get; set; }

    public IncomingShipment IncomingShipment { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
