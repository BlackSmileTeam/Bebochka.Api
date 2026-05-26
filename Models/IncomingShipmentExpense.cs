namespace Bebochka.Api.Models;

/// <summary>
/// Статья расхода по входящей поставке.
/// </summary>
public class IncomingShipmentExpense
{
    public int Id { get; set; }

    /// <summary>
    /// Может быть NULL — расход не привязан к конкретному поступлению.
    /// </summary>
    public int? IncomingShipmentId { get; set; }

    public IncomingShipment? IncomingShipment { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
