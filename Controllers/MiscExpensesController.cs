using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api/misc-expenses")]
[Authorize(Roles = "Admin")]
public class MiscExpensesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MiscExpensesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<MiscExpenseDto>>> GetAll()
    {
        var rows = await _context.IncomingShipmentExpenses
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return Ok(rows.Select(MapToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<MiscExpenseDto>> Create([FromBody] CreateMiscExpenseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Введите название расхода." });
        if (dto.Amount <= 0)
            return BadRequest(new { message = "Сумма должна быть больше нуля." });

        var entity = new IncomingShipmentExpense
        {
            Name = dto.Name.Trim(),
            Amount = dto.Amount,
            IncomingShipmentId = dto.IncomingShipmentId,
            CreatedAt = DateTime.UtcNow
        };
        _context.IncomingShipmentExpenses.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(MapToDto(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MiscExpenseDto>> Update(int id, [FromBody] UpdateMiscExpenseDto dto)
    {
        var entity = await _context.IncomingShipmentExpenses.FindAsync(id);
        if (entity == null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Введите название расхода." });
        if (dto.Amount <= 0)
            return BadRequest(new { message = "Сумма должна быть больше нуля." });

        entity.Name = dto.Name.Trim();
        entity.Amount = dto.Amount;
        entity.IncomingShipmentId = dto.IncomingShipmentId;
        await _context.SaveChangesAsync();
        return Ok(MapToDto(entity));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.IncomingShipmentExpenses.FindAsync(id);
        if (entity == null) return NotFound();
        _context.IncomingShipmentExpenses.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static MiscExpenseDto MapToDto(IncomingShipmentExpense e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Amount = e.Amount,
        IncomingShipmentId = e.IncomingShipmentId,
        CreatedAt = e.CreatedAt
    };
}
