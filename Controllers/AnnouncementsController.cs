using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;
using Bebochka.Api.Helpers;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for managing announcements
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class AnnouncementsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CollageService _collageService;
    private readonly IProductService _productService;
    private readonly ILogger<AnnouncementsController> _logger;

    public AnnouncementsController(
        AppDbContext context,
        CollageService collageService,
        IProductService productService,
        ILogger<AnnouncementsController> logger)
    {
        _context = context;
        _collageService = collageService;
        _productService = productService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all announcements
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Announcement>>> GetAnnouncements()
    {
        var announcements = await _context.Announcements
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
        return Ok(announcements);
    }

    /// <summary>
    /// Gets all unpublished products for announcement selection
    /// </summary>
    [HttpGet("unpublished-products")]
    public async Task<ActionResult<List<ProductDto>>> GetUnpublishedProducts()
    {
        var products = await _productService.GetUnpublishedProductsAsync();
        return Ok(products);
    }

    /// <summary>
    /// Creates a new announcement
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Announcement>> CreateAnnouncement([FromBody] CreateAnnouncementDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new { message = "Request body is required" });
        }

        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest(new { message = "Message is required" });
        }

        // Check if ScheduledAt is provided and valid
        if (dto.ScheduledAt == default(DateTime))
        {
            return BadRequest(new { message = "Scheduled time is required" });
        }
        
        _logger.LogInformation("Creating announcement. ScheduledAt (UTC): {ScheduledAtUtc}, ProductIds count: {ProductIdsCount}", 
            dto.ScheduledAt, dto.ProductIds?.Count ?? 0);

        // Frontend sends ISO datetime string where the time components represent Moscow time
        // The DateTime from JSON deserialization will be in UTC, but the hour/minute components represent Moscow time
        // We extract the components (year, month, day, hour, minute) and store them as Moscow time
        // MySQL DATETIME doesn't store timezone, so we store the components directly
        var moscowNow = DateTimeHelper.GetMoscowTime();
        
        // When JSON deserializes ISO string, it creates UTC DateTime
        // But since frontend created it with Date.UTC() using Moscow time components,
        // we can extract the components directly
        // Note: dto.ScheduledAt will be in UTC, but the hour/minute represent Moscow time
        var scheduledAtMoscow = new DateTime(
            dto.ScheduledAt.Year,
            dto.ScheduledAt.Month,
            dto.ScheduledAt.Day,
            dto.ScheduledAt.Hour,
            dto.ScheduledAt.Minute,
            dto.ScheduledAt.Second,
            DateTimeKind.Unspecified
        );
        
        if (scheduledAtMoscow < moscowNow)
        {
            return BadRequest(new { message = $"Scheduled time must be in the future. Current Moscow time: {moscowNow:yyyy-MM-dd HH:mm:ss}, Scheduled: {scheduledAtMoscow:yyyy-MM-dd HH:mm:ss}" });
        }

        var announcement = new Announcement
        {
            Message = dto.Message,
            ScheduledAt = scheduledAtMoscow, // Store directly (will be treated as Moscow time during comparison)
            ProductIds = dto.ProductIds,
            CreatedAt = DateTime.UtcNow
        };

        // Create collages from product images if products are selected
        if (dto.ProductIds != null && dto.ProductIds.Count > 0)
        {
            try
            {
                var products = await _context.Products
                    .Where(p => dto.ProductIds.Contains(p.Id))
                    .ToListAsync();

                var imagePaths = new List<string>();
                foreach (var product in products)
                {
                    if (product.Images != null && product.Images.Count > 0)
                    {
                        // Take first image from each product
                        imagePaths.Add(product.Images[0]);
                    }
                }

                // Create collages (4 images per collage)
                var collagePaths = new List<string>();
                for (int i = 0; i < imagePaths.Count; i += 4)
                {
                    var batch = imagePaths.Skip(i).Take(4).ToList();
                    if (batch.Count > 0)
                    {
                        var collagePath = await _collageService.CreateCollageAsync(batch);
                        collagePaths.Add(collagePath);
                    }
                }

                announcement.CollageImages = collagePaths;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating collages for announcement");
                return StatusCode(500, new { message = "Error creating collages", error = ex.Message });
            }
        }

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAnnouncement), new { id = announcement.Id }, announcement);
    }

    /// <summary>
    /// Gets a specific announcement by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Announcement>> GetAnnouncement(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null)
        {
            return NotFound();
        }

        return Ok(announcement);
    }

    /// <summary>
    /// Deletes an announcement
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAnnouncement(int id)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null)
        {
            return NotFound();
        }

        _context.Announcements.Remove(announcement);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

