namespace Bebochka.Api.Models;

/// <summary>
/// Запись в истории смены статуса заказа.
/// </summary>
public class OrderStatusHistory
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public string Status { get; set; } = string.Empty;

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public int? ChangedByUserId { get; set; }

    public User? ChangedByUser { get; set; }
}
