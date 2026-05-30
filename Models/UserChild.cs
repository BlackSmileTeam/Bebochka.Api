namespace Bebochka.Api.Models;

/// <summary>
/// Child profile linked to a user account (for size recommendations and future personalization).
/// </summary>
public class UserChild
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    /// <summary>Current clothing size, e.g. 98, 104, 110.</summary>
    public string ClothingSize { get; set; } = string.Empty;

    /// <summary>мальчик or девочка.</summary>
    public string Gender { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
