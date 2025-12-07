using BCrypt.Net;

namespace Bebochka.Api.Utilities;

/// <summary>
/// Utility class to generate BCrypt password hashes for SQL scripts
/// Run this as a console application or use it in a test
/// </summary>
public static class GeneratePasswordHash
{
    /// <summary>
    /// Generates a BCrypt hash for the given password
    /// </summary>
    /// <param name="password">The plain text password</param>
    /// <returns>The BCrypt hash</returns>
    public static string Generate(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}

