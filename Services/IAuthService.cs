using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

/// <summary>
/// Service interface for authentication operations
/// </summary>
public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(LoginDto loginDto);
    Task<UserDto?> ValidateTokenAsync(string token);
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto?> LoginWithGoogleAsync(GoogleLoginDto dto);
    /// <summary>Завершение входа VK ID (OAuth 2.1 + PKCE, id.vk.ru).</summary>
    Task<(AuthResponseDto? Response, string? ErrorCode)> CompleteVkOAuthAsync(
        string code,
        string? deviceId,
        string oauthState,
        VkIdOAuthPending pending,
        CancellationToken cancellationToken = default);
    Task<bool> SendPhoneLoginCodeAsync(PhoneSendCodeDto dto);
    Task<AuthResponseDto?> VerifyPhoneLoginAsync(PhoneVerifyDto dto);
    Task MergeGuestCartAsync(int userId, string sessionId);
}
