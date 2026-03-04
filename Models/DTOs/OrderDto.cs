namespace Bebochka.Api.Models.DTOs;

/// <summary>
/// Data transfer object for order information
/// </summary>
public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }
    public string? DeliveryMethod { get; set; }
    public string? Comment { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Ожидает оплату";
    public List<OrderItemDto> OrderItems { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public int? UserId { get; set; }
    public long? TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
}

/// <summary>
/// Data transfer object for order item
/// </summary>
public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Data transfer object for creating an order
/// </summary>
public class CreateOrderDto
{
    public string SessionId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }
    public string? DeliveryMethod { get; set; }
    public string? Comment { get; set; }
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// Data transfer object for creating an order item
/// </summary>
public class CreateOrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Request for reserving a product from a Telegram channel post comment.
/// </summary>
public class ReserveFromTelegramRequestDto
{
    public string ChannelId { get; set; } = string.Empty;
    public int MessageId { get; set; }
    public long TelegramUserId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    /// <summary>Телефон, если пользователь поделился контактом в Telegram.</summary>
    public string? CustomerPhone { get; set; }
}

/// <summary>
/// Result of reserve-from-telegram.
/// </summary>
public class ReserveFromTelegramResultDto
{
    public bool Success { get; set; }
    public OrderDto? Order { get; set; }
    public string? Reason { get; set; }
}

