namespace Bebochka.Api.Utilities;

/// <summary>
/// Utility class for password hashing operations
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// Generates a BCrypt hash for a password
    /// </summary>
    /// <param name="password">Plain text password</param>
    /// <returns>BCrypt hash string</returns>
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <summary>
    /// Verifies a password against a hash
    /// </summary>
    /// <param name="password">Plain text password</param>
    /// <param name="hash">BCrypt hash</param>
    /// <returns>True if password matches hash</returns>
    public static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}

