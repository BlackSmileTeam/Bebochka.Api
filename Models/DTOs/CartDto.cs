namespace Bebochka.Api.Models.DTOs;

/// <summary>
/// Data transfer object for adding item to cart
/// </summary>
public class AddToCartDto
{
    /// <summary>
    /// Gets or sets the session ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the product ID
    /// </summary>
    public int ProductId { get; set; }
    
    /// <summary>
    /// Gets or sets the quantity to add
    /// </summary>
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// Data transfer object for updating cart item
/// </summary>
public class UpdateCartItemDto
{
    /// <summary>
    /// Gets or sets the new quantity
    /// </summary>
    public int Quantity { get; set; }
}

/// <summary>
/// Data transfer object for cart item
/// </summary>
public class CartItemDto
{
    /// <summary>
    /// Gets or sets the cart item ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the product ID
    /// </summary>
    public int ProductId { get; set; }
    
    /// <summary>
    /// Gets or sets the product name
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the product price
    /// </summary>
    public decimal ProductPrice { get; set; }
    
    /// <summary>
    /// Gets or sets the product brand
    /// </summary>
    public string? ProductBrand { get; set; }
    
    /// <summary>
    /// Gets or sets the product size
    /// </summary>
    public string? ProductSize { get; set; }
    
    /// <summary>
    /// Gets or sets the product color
    /// </summary>
    public string? ProductColor { get; set; }
    
    /// <summary>
    /// Gets or sets the product images
    /// </summary>
    public List<string> ProductImages { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the quantity
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Gets or sets the creation date
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

