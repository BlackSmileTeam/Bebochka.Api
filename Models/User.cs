namespace Bebochka.Api.Models;

/// <summary>
/// Represents an admin user in the system
/// </summary>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier of the user
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the username for login
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the hashed password
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the user's email address
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// Gets or sets the user's full name
    /// </summary>
    public string? FullName { get; set; }
    
    /// <summary>
    /// Gets or sets whether the user is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether the user is an administrator
    /// </summary>
    public bool IsAdmin { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the date and time when the user was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the date and time when the user last logged in
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Gets or sets the Telegram User ID for linking with Telegram bot
    /// </summary>
    public long? TelegramUserId { get; set; }
}

