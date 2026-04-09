using System.Text.Json.Serialization;

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

    public int UserId { get; set; }
    public bool IsAdmin { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Регистрация покупателя по телефону (логин в системе генерируется автоматически).
/// </summary>
public class RegisterDto
{
    /// <summary>Телефон в произвольном виде; нормализуется в E.164.</summary>
    public string Phone { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FullName { get; set; }

    /// <summary>Обязательное согласие с пользовательским соглашением и обработкой персональных данных.</summary>
    public bool AcceptPersonalDataProcessing { get; set; }
}

/// <summary>
/// Вход через Google (credential JWT с фронта)
/// </summary>
public class GoogleLoginDto
{
    [JsonPropertyName("idToken")]
    public string IdToken { get; set; } = string.Empty;

    /// <summary>Обязательно при первичной регистрации через Google.</summary>
    [JsonPropertyName("acceptPersonalDataProcessing")]
    public bool AcceptPersonalDataProcessing { get; set; }
}

/// <summary>
/// Запрос кода на телефон
/// </summary>
public class PhoneSendCodeDto
{
    public string Phone { get; set; } = string.Empty;
}

/// <summary>
/// Проверка кода и вход
/// </summary>
public class PhoneVerifyDto
{
    public string Phone { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>Обязательно при первичной регистрации по телефону.</summary>
    public bool AcceptPersonalDataProcessing { get; set; }
}

/// <summary>
/// Слияние гостевой корзины после входа
/// </summary>
public class MergeCartDto
{
    public string SessionId { get; set; } = string.Empty;
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
    /// Телефон в E.164
    /// </summary>
    public string? Phone { get; set; }
    
    /// <summary>
    /// Gets or sets the full name
    /// </summary>
    public string? FullName { get; set; }
    
    /// <summary>
    /// Gets or sets the creation date
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets preferred custom emoji id for channel posts (Telegram custom_emoji_id)
    /// </summary>
    public string? ChannelCustomEmojiId { get; set; }

    /// <summary>
    /// Администратор (доступ в админку)
    /// </summary>
    public bool IsAdmin { get; set; }
}

/// <summary>
/// Data transfer object for changing password
/// </summary>
public class ChangePasswordDto
{
    /// <summary>
    /// Gets or sets the new password
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object for linking Telegram User ID
/// </summary>
public class LinkTelegramUserIdDto
{
    /// <summary>
    /// Gets or sets the Telegram User ID
    /// </summary>
    public long TelegramUserId { get; set; }
}

/// <summary>
/// Request DTO for updating current user's channel emoji preference
/// </summary>
public class UpdateChannelEmojiDto
{
    /// <summary>
    /// Gets or sets the Telegram custom_emoji_id to use in channel posts (null/empty to reset)
    /// </summary>
    public string? EmojiId { get; set; }
}

