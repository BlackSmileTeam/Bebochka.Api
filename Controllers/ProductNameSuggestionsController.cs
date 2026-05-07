using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductNameSuggestionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductNameSuggestionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductNameSuggestion>>> GetSuggestions([FromQuery] string? search = null)
    {
        var query = _context.ProductNameSuggestions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var names = await query.OrderBy(x => x.Name).ToListAsync();
        return Ok(names);
    }
}

