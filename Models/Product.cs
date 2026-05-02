using System.Text.Json;

namespace Bebochka.Api.Models;

/// <summary>
/// Represents a product in the Bebochka store
/// </summary>
public class Product
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
    /// Gets or sets the JSON string representation of product images stored in the database
    /// </summary>
    public string ImagesJson { get; set; } = "[]";
    
    /// <summary>
    /// Gets or sets the list of product image paths
    /// This property serializes/deserializes to/from ImagesJson for database storage
    /// </summary>
    public List<string> Images
    {
        get => string.IsNullOrEmpty(ImagesJson) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(ImagesJson) ?? new List<string>();
        set => ImagesJson = JsonSerializer.Serialize(value ?? new List<string>());
    }
    
    /// <summary>
    /// Gets or sets the JSON string of Telegram file_id for each image (pre-cached for fast publish).
    /// </summary>
    public string? TelegramFileIdsJson { get; set; }
    
    /// <summary>
    /// Gets or sets the list of Telegram file_id for each image (same order as Images).
    /// When set, channel send uses these instead of re-uploading.
    /// </summary>
    public List<string>? TelegramFileIds
    {
        get => string.IsNullOrEmpty(TelegramFileIdsJson) 
            ? null 
            : JsonSerializer.Deserialize<List<string>>(TelegramFileIdsJson);
        set => TelegramFileIdsJson = value == null || value.Count == 0 
            ? null 
            : JsonSerializer.Serialize(value);
    }
    
    /// <summary>
    /// Gets or sets the date and time when the product was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the date and time when the product was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
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
    
    /// <summary>
    /// Gets or sets the date and time when the product should be published and become visible in catalog
    /// If null, product is published immediately
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Moscow time: "В корзину" is enabled at or after this moment. If null, same as catalog visibility rules.
    /// </summary>
    public DateTime? CartAvailableAt { get; set; }

    /// <summary>
    /// Telegram channel message ID of the post (for linking comments/reservations).
    /// </summary>
    public int? TelegramMessageId { get; set; }

    /// <summary>
    /// Telegram channel/chat ID (e.g. -1001234567890) where the product was posted.
    /// Stored as string to avoid issues with different ID formats.
    /// </summary>
    public string? TelegramChatId { get; set; }

    /// <summary>
    /// Gets or sets box identifier where product is currently stored (admin only field).
    /// </summary>
    public string? BoxNumber { get; set; }

    /// <summary>
    /// Gets or sets incoming shipment identifier from which product arrived.
    /// </summary>
    public int? IncomingShipmentId { get; set; }

    /// <summary>
    /// Navigation property to incoming shipment.
    /// </summary>
    public IncomingShipment? IncomingShipment { get; set; }
}
