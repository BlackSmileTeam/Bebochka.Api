namespace Bebochka.Api.Models;

/// <summary>
/// Represents an item in a user's shopping cart
/// </summary>
public class CartItem
{
    /// <summary>
    /// Gets or sets the unique identifier of the cart item
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the session ID of the user (from localStorage or cookie)
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Registered user id when cart is tied to account; null for guests
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Navigation to user
    /// </summary>
    public User? User { get; set; }
    
    /// <summary>
    /// Gets or sets the product ID
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>Комплект, если позиция относится к комплекту.</summary>
    public int? KitId { get; set; }

    public ProductKit? Kit { get; set; }

    /// <summary>part — одна вещь; bundle — весь комплект.</summary>
    public string? CartAddMode { get; set; }

    /// <summary>Группирует строки одного добавления комплекта целиком.</summary>
    public string? KitBundleKey { get; set; }

    /// <summary>Цена за единицу в корзине (комплект или часть).</summary>
    public decimal? ChargedUnitPrice { get; set; }
    
    /// <summary>
    /// Gets or sets the product navigation property
    /// </summary>
    public Product? Product { get; set; }
    
    /// <summary>
    /// Gets or sets the quantity of items in the cart
    /// </summary>
    public int Quantity { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the date and time when the cart item was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the date and time when the cart item was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

