using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

/// <summary>
/// Service interface for authentication operations
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a user and returns a JWT token
    /// </summary>
    /// <param name="loginDto">Login credentials</param>
    /// <returns>Authentication response with token</returns>
    Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
    
    /// <summary>
    /// Validates a JWT token and returns user information
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>User information if token is valid</returns>
    Task<UserDto?> ValidateTokenAsync(string token);
}

