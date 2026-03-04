namespace Bebochka.Api.Models;

/// <summary>
/// Represents an item in an order
/// </summary>
public class OrderItem
{
    /// <summary>
    /// Gets or sets the unique identifier of the order item
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the order ID
    /// </summary>
    public int OrderId { get; set; }
    
    /// <summary>
    /// Gets or sets the order navigation property
    /// </summary>
    public Order? Order { get; set; }
    
    /// <summary>
    /// Gets or sets the product ID
    /// </summary>
    public int ProductId { get; set; }
    
    /// <summary>
    /// Gets or sets the product navigation property
    /// </summary>
    public Product? Product { get; set; }
    
    /// <summary>
    /// Gets or sets the product name at the time of order
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the product price at the time of order
    /// </summary>
    public decimal ProductPrice { get; set; }
    
    /// <summary>
    /// Gets or sets the quantity of items
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the order item was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Telegram chat ID of the user's "беру" comment (to delete when item is removed from order).
    /// </summary>
    public long? TelegramCommentChatId { get; set; }

    /// <summary>
    /// Telegram message ID of the user's "беру" comment.
    /// </summary>
    public int? TelegramCommentMessageId { get; set; }

    /// <summary>
    /// Whether the item has been added to the parcel (for packing checklist).
    /// </summary>
    public bool AddedToParcel { get; set; }
}

