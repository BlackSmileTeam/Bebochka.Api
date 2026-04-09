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
    Task<bool> SendPhoneLoginCodeAsync(PhoneSendCodeDto dto);
    Task<AuthResponseDto?> VerifyPhoneLoginAsync(PhoneVerifyDto dto);
    Task MergeGuestCartAsync(int userId, string sessionId);
}
