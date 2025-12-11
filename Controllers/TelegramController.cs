using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Services;
using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for sending messages via Telegram bot
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TelegramController : ControllerBase
{
    private readonly ITelegramNotificationService _telegramService;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        ITelegramNotificationService telegramService,
        ILogger<TelegramController> logger)
    {
        _telegramService = telegramService;
        _logger = logger;
    }

    /// <summary>
    /// Sends a broadcast message to all Telegram bot users
    /// </summary>
    /// <param name="request">Message request</param>
    /// <returns>Result with number of users who received the message</returns>
    /// <response code="200">Message sent successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("broadcast")]
    [ProducesResponseType(typeof(BroadcastMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BroadcastMessageResponseDto>> SendBroadcast([FromBody] BroadcastMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message text is required" });
        }

        try
        {
            var sentCount = await _telegramService.SendBroadcastMessageAsync(request.Message);
            
            return Ok(new BroadcastMessageResponseDto
            {
                Success = true,
                SentCount = sentCount,
                Message = "Broadcast message sent successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending broadcast message");
            return StatusCode(500, new { message = "Error sending broadcast message", error = ex.Message });
        }
    }

    /// <summary>
    /// Sends a message to a specific Telegram user
    /// </summary>
    /// <param name="chatId">Telegram chat ID</param>
    /// <param name="request">Message request</param>
    /// <returns>Result indicating success or failure</returns>
    /// <response code="200">Message sent successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("send/{chatId}")]
    [ProducesResponseType(typeof(SendMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendMessageResponseDto>> SendMessage(long chatId, [FromBody] SendMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message text is required" });
        }

        try
        {
            var success = await _telegramService.SendMessageAsync(chatId, request.Message);
            
            return Ok(new SendMessageResponseDto
            {
                Success = success,
                Message = success ? "Message sent successfully" : "Failed to send message"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to chat {ChatId}", chatId);
            return StatusCode(500, new { message = "Error sending message", error = ex.Message });
        }
    }
}

/// <summary>
/// Request DTO for broadcast message
/// </summary>
public class BroadcastMessageRequestDto
{
    /// <summary>
    /// Gets or sets the message text to send
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for broadcast message
/// </summary>
public class BroadcastMessageResponseDto
{
    /// <summary>
    /// Gets or sets whether the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the number of users who received the message
    /// </summary>
    public int SentCount { get; set; }
    
    /// <summary>
    /// Gets or sets the response message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for sending message to specific user
/// </summary>
public class SendMessageRequestDto
{
    /// <summary>
    /// Gets or sets the message text to send
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for sending message
/// </summary>
public class SendMessageResponseDto
{
    /// <summary>
    /// Gets or sets whether the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the response message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

