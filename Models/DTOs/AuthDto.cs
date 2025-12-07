namespace Bebochka.Api.Models.DTOs;

/// <summary>
/// Data transfer object for login request
/// </summary>
public class LoginDto
{
    /// <summary>
    /// Gets or sets the username
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the password
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object for authentication response
/// </summary>
public class AuthResponseDto
{
    /// <summary>
    /// Gets or sets the JWT token
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the token expiration time
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Gets or sets the username
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the user's full name
    /// </summary>
    public string? FullName { get; set; }
}

/// <summary>
/// Data transfer object for user information
/// </summary>
public class UserDto
{
    /// <summary>
    /// Gets or sets the user ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the username
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the email
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// Gets or sets the full name
    /// </summary>
    public string? FullName { get; set; }
}

