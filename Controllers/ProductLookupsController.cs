using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class ProductLookupsController : ControllerBase
{
    private readonly LookupItemsService _lookups;

    public ProductLookupsController(LookupItemsService lookups)
    {
        _lookups = lookups;
    }

    [HttpGet("product-colors")]
    public Task<ActionResult<List<LookupItemDto>>> GetColors([FromQuery] string? search = null)
        => GetList(() => _lookups.GetColorsAsync(search));

    [HttpPost("product-colors")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LookupItemDto>> CreateColor([FromBody] LookupItemCreateDto dto)
    {
        try
        {
            return Ok(await _lookups.CreateColorAsync(dto.Name));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("product-colors/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LookupItemDto>> UpdateColor(int id, [FromBody] LookupItemCreateDto dto)
    {
        try
        {
            var item = await _lookups.UpdateColorAsync(id, dto.Name);
            return item == null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("product-colors/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteColor(int id)
        => await _lookups.DeleteColorAsync(id) ? NoContent() : NotFound();

    [HttpGet("product-conditions")]
    public Task<ActionResult<List<LookupItemDto>>> GetConditions([FromQuery] string? search = null)
        => GetList(() => _lookups.GetConditionsAsync(search));

    [HttpPost("product-conditions")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LookupItemDto>> CreateCondition([FromBody] LookupItemCreateDto dto)
    {
        try
        {
            return Ok(await _lookups.CreateConditionAsync(dto.Name));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("product-conditions/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LookupItemDto>> UpdateCondition(int id, [FromBody] LookupItemCreateDto dto)
    {
        try
        {
            var item = await _lookups.UpdateConditionAsync(id, dto.Name);
            return item == null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("product-conditions/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteCondition(int id)
        => await _lookups.DeleteConditionAsync(id) ? NoContent() : NotFound();

    [HttpGet("product-nuances")]
    public Task<ActionResult<List<LookupItemDto>>> GetNuances([FromQuery] string? search = null)
        => GetList(() => _lookups.GetNuancesAsync(search));

    [HttpPost("product-nuances")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LookupItemDto>> CreateNuance([FromBody] LookupItemCreateDto dto)
    {
        try
        {
            return Ok(await _lookups.CreateNuanceAsync(dto.Name));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("product-nuances/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LookupItemDto>> UpdateNuance(int id, [FromBody] LookupItemCreateDto dto)
    {
        try
        {
            var item = await _lookups.UpdateNuanceAsync(id, dto.Name);
            return item == null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("product-nuances/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteNuance(int id)
        => await _lookups.DeleteNuanceAsync(id) ? NoContent() : NotFound();

    private async Task<ActionResult<List<LookupItemDto>>> GetList(Func<Task<List<LookupItemDto>>> loader)
        => Ok(await loader());
}
