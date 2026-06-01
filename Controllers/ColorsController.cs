using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
using Bebochka.Api.Services;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ColorsController : ControllerBase
{
    private readonly LookupItemsService _lookups;

    public ColorsController(LookupItemsService lookups)
    {
        _lookups = lookups;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [EnableCors("AllowReactApp")]
    public async Task<IActionResult> GetColors()
    {
        var items = await _lookups.GetColorsAsync(search: null);
        var names = items.Select(i => i.Name).ToList();
        return new JsonResult(names) { StatusCode = 200 };
    }
}
