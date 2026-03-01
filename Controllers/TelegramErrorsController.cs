using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for managing Telegram channel sending errors
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TelegramErrorsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<TelegramErrorsController> _logger;

    public TelegramErrorsController(AppDbContext context, ILogger<TelegramErrorsController> logger)
    {
        _context = context;
        _logger = logger;
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
