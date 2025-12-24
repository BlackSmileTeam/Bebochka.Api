using System.Text.Json;

namespace Bebochka.Api.Models;

/// <summary>
/// Represents a scheduled announcement to be sent to Telegram users
/// </summary>
public class Announcement
{
    /// <summary>
    /// Gets or sets the unique identifier of the announcement
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the announcement message text
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the scheduled send time (UTC)
    /// </summary>
    public DateTime ScheduledAt { get; set; }
    
    /// <summary>
    /// Gets or sets the JSON string representation of product IDs for collage
    /// </summary>
    public string ProductIdsJson { get; set; } = "[]";
    
    /// <summary>
    /// Gets or sets the list of product IDs to include in collage
    /// This property serializes/deserializes to/from ProductIdsJson for database storage
    /// </summary>
    public List<int> ProductIds
    {
        get => string.IsNullOrEmpty(ProductIdsJson) 
            ? new List<int>() 
            : JsonSerializer.Deserialize<List<int>>(ProductIdsJson) ?? new List<int>();
        set => ProductIdsJson = JsonSerializer.Serialize(value ?? new List<int>());
    }
    
    /// <summary>
    /// Gets or sets the JSON string representation of collage image paths
    /// </summary>
    public string CollageImagesJson { get; set; } = "[]";
    
    /// <summary>
    /// Gets or sets the list of collage image paths
    /// This property serializes/deserializes to/from CollageImagesJson for database storage
    /// </summary>
    public List<string> CollageImages
    {
        get => string.IsNullOrEmpty(CollageImagesJson) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(CollageImagesJson) ?? new List<string>();
        set => CollageImagesJson = JsonSerializer.Serialize(value ?? new List<string>());
    }
    
    /// <summary>
    /// Gets or sets whether the announcement has been sent
    /// </summary>
    public bool IsSent { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the date and time when the announcement was sent (if sent)
    /// </summary>
    public DateTime? SentAt { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the announcement was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the number of users the announcement was sent to
    /// </summary>
    public int SentCount { get; set; } = 0;
}

