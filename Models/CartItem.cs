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
    /// Gets or sets the product ID
    /// </summary>
    public int ProductId { get; set; }
    
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

