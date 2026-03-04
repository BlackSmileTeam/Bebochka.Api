using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Models;
using Bebochka.Api.Services;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Telegram webhook controller that handles comments under channel posts and creates reservations via backend only.
/// </summary>
[ApiController]
[Route("api/Telegram/webhook")]
public class TelegramWebhookController : ControllerBase
{
    private static readonly string[] ReserveWords = { "мне", "я", "беру", "бронь" };

    private readonly IOrderService _orderService;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        IOrderService orderService,
        ILogger<TelegramWebhookController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Entry point for Telegram Bot API webhook.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] TelegramUpdateDto update)
    {
        try
        {
            var message = update.Message ?? update.ChannelPost;
            if (message == null)
                return Ok(); // nothing to do

            var text = (message.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
                return Ok();

            var from = message.From;
            if (from == null || from.Id == 0)
                return Ok();

            // Only handle replies with allowed reserve words
            if (message.ReplyToMessage == null)
                return Ok();

            var textLower = text.ToLowerInvariant();
            if (!ReserveWords.Any(w => textLower.Contains(w)))
                return Ok();

            var (channelId, messageId) = GetChannelPostFromReply(message.ReplyToMessage);
            if (channelId == null || messageId == null)
            {
                _logger.LogWarning("ReserveFromTelegram: cannot determine channelId/messageId from reply");
                return Ok();
            }

            _logger.LogInformation("ReserveFromTelegram attempt: ChannelId={ChannelId}, MessageId={MessageId}, TelegramUserId={UserId}",
                channelId, messageId.Value, from.Id);

            var phone = message.Contact?.PhoneNumber;
            var commentChatId = message.Chat?.Id ?? 0;
            var commentMessageId = message.MessageId;
            var result = await _orderService.ReserveFromTelegramAsync(
                channelId,
                messageId.Value,
                from.Id,
                from.Username,
                from.FirstName,
                from.LastName,
                customerPhone: phone,
                commentChatId: commentChatId != 0 ? commentChatId : null,
                commentMessageId: commentMessageId);

            // При обнаружении кодовой фразы ничего не пишем в чат — только создаём заказ при успехе
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telegram webhook update");
        }

        // Always respond 200 OK so Telegram does not retry endlessly
        return Ok();
    }

    /// <summary>
    /// Из ответа на сообщение получает идентификаторы поста в канале (channelId, messageId) для привязки к товару.
    /// Если ответ в канале — берём Chat.Id и MessageId. Если ответ в группе обсуждения — берём канал и message_id из полей пересылки.
    /// </summary>
    private static (string? channelId, int? messageId) GetChannelPostFromReply(TelegramMessageDto replyToMessage)
    {
        // 1. Прямой ответ в канале под постом
        if (replyToMessage.Chat != null && replyToMessage.Chat.Id != 0)
            return (replyToMessage.Chat.Id.ToString(), replyToMessage.MessageId);

        // 2. Ответ в группе обсуждения: сообщение переслано из канала
        if (replyToMessage.ForwardFromChat != null && replyToMessage.ForwardFromMessageId.HasValue)
            return (replyToMessage.ForwardFromChat.Id.ToString(), replyToMessage.ForwardFromMessageId.Value);

        // 3. Попробуем достать из forward_origin (Bot API 7+)
        if (replyToMessage.ForwardOrigin.HasValue)
        {
            try
            {
                var origin = replyToMessage.ForwardOrigin.Value;
                if (origin.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (origin.TryGetProperty("chat", out var chat) &&
                        chat.TryGetProperty("id", out var cid) &&
                        origin.TryGetProperty("message_id", out var mid))
                    {
                        var chatId = cid.ValueKind == System.Text.Json.JsonValueKind.Number
                            ? cid.GetInt64().ToString()
                            : cid.GetString();
                        var msgId = mid.GetInt32();
                        if (!string.IsNullOrEmpty(chatId))
                            return (chatId, msgId);
                    }
                }
            }
            catch
            {
                // ignore parsing errors
            }
        }

        return (null, null);
    }
}

