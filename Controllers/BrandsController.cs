using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for managing brands
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BrandsController : ControllerBase
{
    private readonly AppDbContext _context;

    public BrandsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all brands
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Brand>>> GetBrands([FromQuery] string? search = null)
    {
        var query = _context.Brands.AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case-insensitive search - MySQL utf8mb4_unicode_ci collation handles this automatically
            query = query.Where(b => b.Name.Contains(search));
        }
        
        var brands = await query.OrderBy(b => b.Name).ToListAsync();
        return Ok(brands);
    }

    /// <summary>
    /// Creates a new brand (admin only)
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Brand>> CreateBrand([FromBody] Brand brand)
    {
        if (string.IsNullOrWhiteSpace(brand.Name))
        {
            return BadRequest(new { message = "Brand name is required" });
        }

        // Check if brand already exists
        if (await _context.Brands.AnyAsync(b => b.Name == brand.Name))
        {
            return BadRequest(new { message = "Brand already exists" });
        }

        brand.CreatedAt = DateTime.UtcNow;
        _context.Brands.Add(brand);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBrand), new { id = brand.Id }, brand);
    }

    /// <summary>
    /// Gets a specific brand by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Brand>> GetBrand(int id)
    {
        var brand = await _context.Brands.FindAsync(id);
        if (brand == null)
        {
            return NotFound();
        }

        return Ok(brand);
    }
}

