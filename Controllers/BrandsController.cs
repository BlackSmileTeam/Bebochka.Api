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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Brand>> CreateBrand([FromBody] Brand brand)
    {
        var name = brand.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Укажите название бренда" });
        if (name.Length > 100)
            return BadRequest(new { message = "Название слишком длинное" });

        if (await _context.Brands.AnyAsync(b => b.Name.ToLower() == name.ToLower()))
            return BadRequest(new { message = "Такой бренд уже существует" });

        var entity = new Brand { Name = name };
        _context.Brands.Add(entity);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBrand), new { id = entity.Id }, entity);
    }

    /// <summary>
    /// Updates a brand (admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Brand>> UpdateBrand(int id, [FromBody] Brand brand)
    {
        var entity = await _context.Brands.FindAsync(id);
        if (entity == null) return NotFound();

        var name = brand.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Укажите название бренда" });
        if (name.Length > 100)
            return BadRequest(new { message = "Название слишком длинное" });

        if (await _context.Brands.AnyAsync(b => b.Id != id && b.Name.ToLower() == name.ToLower()))
            return BadRequest(new { message = "Такой бренд уже существует" });

        entity.Name = name;
        await _context.SaveChangesAsync();
        return Ok(entity);
    }

    /// <summary>
    /// Deletes a brand (admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteBrand(int id)
    {
        var entity = await _context.Brands.FindAsync(id);
        if (entity == null) return NotFound();

        _context.Brands.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
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

