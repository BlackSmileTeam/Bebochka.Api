namespace Bebochka.Api.Models.DTOs;

/// <summary>
/// Data transfer object for creating an announcement
/// </summary>
public class CreateAnnouncementDto
{
    /// <summary>
    /// Gets or sets the announcement message text
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the scheduled send time (in Moscow time, will be converted to UTC)
    /// </summary>
    public DateTime ScheduledAt { get; set; }
    
    /// <summary>
    /// Gets or sets the list of product IDs to include in collage
    /// </summary>
    public List<int> ProductIds { get; set; } = new();
}

