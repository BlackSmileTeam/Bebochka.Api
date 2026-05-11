namespace Bebochka.Api.Models;

/// <summary>
/// Отзыв клиента по заказу (не более одного на заказ).
/// </summary>
public class OrderCustomerReview
{
    public int Id { get; set; }

    /// <summary>Заказ; null — отзыв только с сайта админом без привязки к заказу.</summary>
    public int? OrderId { get; set; }

    public Order? Order { get; set; }

    /// <summary>Имя/телефон для ручного отзыва без заказа (иначе из заказа/пользователя).</summary>
    public string? ManualCustomerName { get; set; }

    public string? ManualCustomerPhone { get; set; }

    public int? UserId { get; set; }

    public User? User { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>JSON-массив путей к фото отзыва (как /uploads/...).</summary>
    public string? ReviewImagesJson { get; set; }
}
