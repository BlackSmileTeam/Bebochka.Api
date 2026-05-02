namespace Bebochka.Api.Models;

/// <summary>
/// Отзыв клиента по заказу (не более одного на заказ).
/// </summary>
public class OrderCustomerReview
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int? UserId { get; set; }

    public User? User { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
