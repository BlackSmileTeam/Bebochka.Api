using System.Text.Json.Serialization;

namespace Bebochka.Api.Models;

/// <summary>
/// Minimal Telegram Bot API update DTO for webhook handling.
/// </summary>
public class TelegramUpdateDto
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageDto? Message { get; set; }

    [JsonPropertyName("channel_post")]
    public TelegramMessageDto? ChannelPost { get; set; }
}

public class TelegramMessageDto
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChatDto Chat { get; set; } = null!;

    [JsonPropertyName("from")]
    public TelegramUserDto? From { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("reply_to_message")]
    public TelegramMessageDto? ReplyToMessage { get; set; }

    // Legacy forwarding fields: used when comments идут из группы обсуждения
    [JsonPropertyName("forward_from_chat")]
    public TelegramChatDto? ForwardFromChat { get; set; }

    [JsonPropertyName("forward_from_message_id")]
    public int? ForwardFromMessageId { get; set; }

    // Generic forward_origin for Bot API 7+ (we keep as raw JSON to extract channel/message_id when possible)
    [JsonPropertyName("forward_origin")]
    public System.Text.Json.JsonElement? ForwardOrigin { get; set; }
}

public class TelegramChatDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class TelegramUserDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
}

