namespace Bebochka.Api.Models;

/// <summary>
/// Represents a brand in the system
/// </summary>
public class Brand
{
    /// <summary>
    /// Gets or sets the unique identifier of the brand
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the brand name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the date and time when the brand was added
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

