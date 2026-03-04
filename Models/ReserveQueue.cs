namespace Bebochka.Api.Models;

/// <summary>
/// Очередь пользователей, написавших «беру» под постом, когда товар уже забронирован.
/// При снятии товара с заказа следующий в очереди получит товар в свой заказ.
/// </summary>
public class ReserveQueue
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public int PostMessageId { get; set; }
    public long TelegramUserId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CustomerPhone { get; set; }
    public long CommentChatId { get; set; }
    public int CommentMessageId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
