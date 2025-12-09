namespace Bebochka.Api.Models;

/// <summary>
/// Represents an order in the system
/// </summary>
public class Order
{
    /// <summary>
    /// Gets or sets the unique identifier of the order
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the user ID who created the order
    /// </summary>
    public int? UserId { get; set; }
    
    /// <summary>
    /// Gets or sets the order number
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the customer name
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the customer phone number
    /// </summary>
    public string CustomerPhone { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the customer email
    /// </summary>
    public string? CustomerEmail { get; set; }
    
    /// <summary>
    /// Gets or sets the customer address
    /// </summary>
    public string? CustomerAddress { get; set; }
    
    /// <summary>
    /// Gets or sets the delivery method
    /// </summary>
    public string? DeliveryMethod { get; set; }
    
    /// <summary>
    /// Gets or sets the order comment
    /// </summary>
    public string? Comment { get; set; }
    
    /// <summary>
    /// Gets or sets the total amount of the order
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Gets or sets the order status (В сборке, Ожидает оплату, В пути, Доставлен, Отменен)
    /// </summary>
    public string Status { get; set; } = "В сборке";
    
    /// <summary>
    /// Gets or sets the list of order items
    /// </summary>
    public List<OrderItem> OrderItems { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the date and time when the order was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the date and time when the order was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the date and time when the order was cancelled
    /// </summary>
    public DateTime? CancelledAt { get; set; }
    
    /// <summary>
    /// Gets or sets the reason for order cancellation
    /// </summary>
    public string? CancellationReason { get; set; }
}

