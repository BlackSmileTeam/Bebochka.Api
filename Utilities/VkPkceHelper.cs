using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Bebochka.Api.Utilities;

/// <summary>
/// PKCE и state для OAuth 2.1 VK ID (<see href="https://id.vk.com/about/business/go/docs/ru/vkid/latest/vk-id/connection/api-description"/>).
/// </summary>
public static class VkPkceHelper
{
    public static string CreateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    /// <summary>State: случайная строка ≥32 символов (рекомендация VK ID).</summary>
    public static string CreateOAuthState()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static string CreateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return WebEncoders.Base64UrlEncode(hash);
    }
}
