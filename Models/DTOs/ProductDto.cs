namespace Bebochka.Api.Models.DTOs;

/// <summary>
/// Data transfer object for product information
/// </summary>
public class ProductDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the product
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the product name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the brand name of the product
    /// </summary>
    public string? Brand { get; set; }
    
    /// <summary>
    /// Gets or sets the product description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the product price
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// Gets or sets the product size
    /// </summary>
    public string? Size { get; set; }
    
    /// <summary>
    /// Gets or sets the product color
    /// </summary>
    public string? Color { get; set; }
    
    /// <summary>
    /// Gets or sets the list of product image paths
    /// </summary>
    public List<string> Images { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the date and time when the product was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the product was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Gets or sets the quantity of products in stock
    /// </summary>
    public int QuantityInStock { get; set; }
    
    /// <summary>
    /// Gets or sets the available quantity (QuantityInStock minus reserved in other carts)
    /// </summary>
    public int AvailableQuantity { get; set; }
    
    /// <summary>
    /// Gets or sets the gender target (мальчик, девочка, унисекс)
    /// </summary>
    public string? Gender { get; set; }
    
    /// <summary>
    /// Gets or sets the product condition (новая, отличное, недостаток)
    /// </summary>
    public string? Condition { get; set; }
}

/// <summary>
/// Data transfer object for creating a new product
/// </summary>
public class CreateProductDto
{
    /// <summary>
    /// Gets or sets the product name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the brand name of the product
    /// </summary>
    public string? Brand { get; set; }
    
    /// <summary>
    /// Gets or sets the product description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the product price
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// Gets or sets the product size
    /// </summary>
    public string? Size { get; set; }
    
    /// <summary>
    /// Gets or sets the product color
    /// </summary>
    public string? Color { get; set; }
    
    /// <summary>
    /// Gets or sets the list of base64 encoded images
    /// </summary>
    public List<string>? Images { get; set; }
    
    /// <summary>
    /// Gets or sets the quantity of products in stock
    /// </summary>
    public int QuantityInStock { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the gender target (мальчик, девочка, унисекс)
    /// </summary>
    public string? Gender { get; set; }
    
    /// <summary>
    /// Gets or sets the product condition (новая, отличное, недостаток)
    /// </summary>
    public string? Condition { get; set; }
}

/// <summary>
/// Data transfer object for updating an existing product
/// </summary>
public class UpdateProductDto
{
    /// <summary>
    /// Gets or sets the product name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the brand name of the product
    /// </summary>
    public string? Brand { get; set; }
    
    /// <summary>
    /// Gets or sets the product description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the product price
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// Gets or sets the product size
    /// </summary>
    public string? Size { get; set; }
    
    /// <summary>
    /// Gets or sets the product color
    /// </summary>
    public string? Color { get; set; }
    
    /// <summary>
    /// Gets or sets the list of existing image paths to preserve
    /// </summary>
    public List<string>? ExistingImages { get; set; }
    
    /// <summary>
    /// Gets or sets the list of new product images as base64 strings
    /// </summary>
    public List<string>? Images { get; set; }
    
    /// <summary>
    /// Gets or sets the quantity of products in stock
    /// </summary>
    public int QuantityInStock { get; set; } = 1;
    
    /// <summary>
    /// Gets or sets the gender target (мальчик, девочка, унисекс)
    /// </summary>
    public string? Gender { get; set; }
    
    /// <summary>
    /// Gets or sets the product condition (новая, отличное, недостаток)
    /// </summary>
    public string? Condition { get; set; }
}
