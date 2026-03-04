using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for managing Telegram channel sending errors and webhook diagnostics
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TelegramErrorsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<TelegramErrorsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public TelegramErrorsController(
        AppDbContext context,
        ILogger<TelegramErrorsController> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Gets all errors grouped by date
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, List<TelegramErrorDto>>>> GetErrors()
    {
        try
        {
            var errors = await _context.TelegramErrors
                .OrderByDescending(e => e.ErrorDate)
                .ToListAsync();

            var groupedErrors = errors
                .GroupBy(e => e.ErrorDate.Date)
                .ToDictionary(
                    g => g.Key.ToString("yyyy-MM-dd"),
                    g => g.Select(e => new TelegramErrorDto
                    {
                        Id = e.Id,
                        ErrorDate = e.ErrorDate,
                        Message = e.Message,
                        Details = e.Details,
                        ErrorType = e.ErrorType,
                        ProductInfo = e.ProductInfo,
                        ImageCount = e.ImageCount,
                        ChannelId = e.ChannelId
                    }).ToList()
                );

            return Ok(groupedErrors);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            // Table might not exist - return empty result
            _logger.LogWarning(dbEx, "TelegramErrors table may not exist. Returning empty result. Error: {Message}", dbEx.Message);
            return Ok(new Dictionary<string, List<TelegramErrorDto>>());
        }
        catch (Exception ex) when (ex.Message.Contains("doesn't exist") || ex.Message.Contains("Table") || ex.Message.Contains("Unknown table"))
        {
            // Table doesn't exist - return empty result
            _logger.LogWarning(ex, "TelegramErrors table does not exist. Returning empty result. Please run create_telegram_errors_table.sql");
            return Ok(new Dictionary<string, List<TelegramErrorDto>>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Telegram errors: {Message}, StackTrace: {StackTrace}", 
                ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = "Failed to fetch errors", error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes an error by ID
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteError(int id)
    {
        try
        {
            var error = await _context.TelegramErrors.FindAsync(id);
            if (error == null)
            {
                return NotFound();
            }

            _context.TelegramErrors.Remove(error);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Telegram error {Id}", id);
            return StatusCode(500, new { message = "Failed to delete error" });
        }
    }

    /// <summary>
    /// Deletes all errors
    /// </summary>
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAllErrors()
    {
        try
        {
            var errors = await _context.TelegramErrors.ToListAsync();
            _context.TelegramErrors.RemoveRange(errors);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all Telegram errors");
            return StatusCode(500, new { message = "Failed to delete all errors" });
        }
    }

    /// <summary>
    /// Returns Telegram webhook diagnostics (getWebhookInfo) and suggested webhook URL for this API.
    /// Token is not exposed to the client.
    /// </summary>
    [HttpGet("webhook-diagnostics")]
    public async Task<ActionResult<TelegramWebhookDiagnosticsDto>> GetWebhookDiagnostics()
    {
        var token = _configuration["TelegramBot:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return Ok(new TelegramWebhookDiagnosticsDto
            {
                Configured = false,
                Error = "TelegramBot:Token не настроен",
                SuggestedWebhookUrl = BuildSuggestedWebhookUrl()
            });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{token}/getWebhookInfo";
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
            var result = root.TryGetProperty("result", out var resultProp) ? resultProp : (JsonElement?)null;

            string? currentUrl = null;
            int pendingUpdateCount = 0;
            int? lastErrorDate = null;
            string? lastErrorMessage = null;

            if (result.HasValue && result.Value.ValueKind == JsonValueKind.Object)
            {
                var res = result.Value;
                if (res.TryGetProperty("url", out var urlProp))
                    currentUrl = urlProp.GetString();
                if (res.TryGetProperty("pending_update_count", out var pendingProp))
                    pendingUpdateCount = pendingProp.TryGetInt32(out var p) ? p : 0;
                if (res.TryGetProperty("last_error_date", out var errDateProp) && errDateProp.ValueKind != JsonValueKind.Null)
                    lastErrorDate = errDateProp.TryGetInt32(out var d) ? d : null;
                if (res.TryGetProperty("last_error_message", out var errMsgProp))
                    lastErrorMessage = errMsgProp.GetString();
            }

            return Ok(new TelegramWebhookDiagnosticsDto
            {
                Configured = true,
                Ok = ok,
                CurrentWebhookUrl = currentUrl ?? string.Empty,
                PendingUpdateCount = pendingUpdateCount,
                LastErrorDateUnix = lastErrorDate,
                LastErrorMessage = lastErrorMessage ?? string.Empty,
                SuggestedWebhookUrl = BuildSuggestedWebhookUrl()
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Telegram webhook info");
            return Ok(new TelegramWebhookDiagnosticsDto
            {
                Configured = true,
                Error = ex.Message,
                SuggestedWebhookUrl = BuildSuggestedWebhookUrl()
            });
        }
    }

    private string BuildSuggestedWebhookUrl()
    {
        var scheme = Request.Scheme;
        var host = Request.Host.Value;
        return $"{scheme}://{host}/api/Telegram/webhook";
    }
}

/// <summary>
/// DTO for Telegram webhook diagnostics (getWebhookInfo result + suggested URL)
/// </summary>
public class TelegramWebhookDiagnosticsDto
{
    public bool Configured { get; set; }
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string CurrentWebhookUrl { get; set; } = string.Empty;
    public int PendingUpdateCount { get; set; }
    public int? LastErrorDateUnix { get; set; }
    public string LastErrorMessage { get; set; } = string.Empty;
    public string SuggestedWebhookUrl { get; set; } = string.Empty;
}

/// <summary>
/// DTO for Telegram error
/// </summary>
public class TelegramErrorDto
{
    public int Id { get; set; }
    public DateTime ErrorDate { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string? ProductInfo { get; set; }
    public int? ImageCount { get; set; }
    public string? ChannelId { get; set; }
}
