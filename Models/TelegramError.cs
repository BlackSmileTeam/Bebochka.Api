namespace Bebochka.Api.Models;

/// <summary>
/// Represents a Telegram channel sending error
/// </summary>
public class TelegramError
{
    /// <summary>
    /// Gets or sets the error ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the error date
    /// </summary>
    public DateTime ErrorDate { get; set; }

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error details (stack trace, inner exception, etc.)
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the error type (Timeout, NetworkError, ApiError, etc.)
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product name or identifier that failed to send
    /// </summary>
    public string? ProductInfo { get; set; }

    /// <summary>
    /// Gets or sets the number of images that were being sent
    /// </summary>
    public int? ImageCount { get; set; }

    /// <summary>
    /// Gets or sets the channel ID where the error occurred
    /// </summary>
    public string? ChannelId { get; set; }
}
