using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api/incoming-shipments")]
[Authorize(Roles = "Admin")]
public class IncomingShipmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public IncomingShipmentsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<IncomingShipmentDto>>> GetAll()
    {
        var shipments = await _context.IncomingShipments
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
        var expenses = await _context.IncomingShipmentExpenses
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var shipmentIds = shipments.Select(s => s.Id).ToList();
        var revenueByShipment = await (
            from oi in _context.OrderItems
            join o in _context.Orders on oi.OrderId equals o.Id
            join p in _context.Products on oi.ProductId equals p.Id
            where p.IncomingShipmentId != null
                  && shipmentIds.Contains(p.IncomingShipmentId.Value)
                  && o.Status == "Получен"
            group oi by p.IncomingShipmentId!.Value into g
            select new { ShipmentId = g.Key, Revenue = g.Sum(x => x.ProductPrice * x.Quantity) }
        ).ToListAsync();
        var revenueMap = revenueByShipment.ToDictionary(x => x.ShipmentId, x => x.Revenue);
        var expensesMap = expenses
            .GroupBy(e => e.IncomingShipmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return Ok(shipments.Select(s => MapToDto(
            s,
            revenueMap.GetValueOrDefault(s.Id),
            expensesMap.GetValueOrDefault(s.Id, new List<IncomingShipmentExpense>())
        )).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<IncomingShipmentDto>> Create([FromBody] CreateIncomingShipmentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Name is required." });

        var entity = new IncomingShipment
        {
            Name = dto.Name.Trim(),
            WeightKg = dto.WeightKg,
            ItemCount = dto.ItemCount,
            OrderedAmount = dto.OrderedAmount,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.IncomingShipments.Add(entity);
        await _context.SaveChangesAsync();
        await ReplaceExpensesAsync(entity.Id, dto.Expenses);

        var createdExpenses = await _context.IncomingShipmentExpenses
            .Where(e => e.IncomingShipmentId == entity.Id)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, MapToDto(entity, null, createdExpenses));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<IncomingShipmentDto>> Update(int id, [FromBody] UpdateIncomingShipmentDto dto)
    {
        var entity = await _context.IncomingShipments.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name = dto.Name.Trim();
        entity.WeightKg = dto.WeightKg;
        entity.ItemCount = dto.ItemCount;
        entity.OrderedAmount = dto.OrderedAmount;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await ReplaceExpensesAsync(id, dto.Expenses);

        var revenue = await (
            from oi in _context.OrderItems
            join o in _context.Orders on oi.OrderId equals o.Id
            join p in _context.Products on oi.ProductId equals p.Id
            where p.IncomingShipmentId == id && o.Status == "Получен"
            select (decimal?)(oi.ProductPrice * oi.Quantity)
        ).SumAsync();
        var expenses = await _context.IncomingShipmentExpenses
            .Where(e => e.IncomingShipmentId == id)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        return Ok(MapToDto(entity, revenue, expenses));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.IncomingShipments.FindAsync(id);
        if (entity == null) return NotFound();

        var hasProducts = await _context.Products.AnyAsync(p => p.IncomingShipmentId == id);
        if (hasProducts)
        {
            return BadRequest(new
            {
                message = "Shipment is linked to products. Remove links in product cards first."
            });
        }

        _context.IncomingShipments.Remove(entity);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task ReplaceExpensesAsync(int incomingShipmentId, List<CreateIncomingShipmentExpenseDto>? items)
    {
        var prev = await _context.IncomingShipmentExpenses
            .Where(e => e.IncomingShipmentId == incomingShipmentId)
            .ToListAsync();
        if (prev.Count > 0)
            _context.IncomingShipmentExpenses.RemoveRange(prev);

        if (items != null && items.Count > 0)
        {
            var rows = items
                .Where(i => !string.IsNullOrWhiteSpace(i.Name) && i.Amount > 0)
                .Select(i => new IncomingShipmentExpense
                {
                    IncomingShipmentId = incomingShipmentId,
                    Name = i.Name.Trim(),
                    Amount = i.Amount,
                    CreatedAt = DateTime.UtcNow
                });
            _context.IncomingShipmentExpenses.AddRange(rows);
        }

        await _context.SaveChangesAsync();
    }

    private static IncomingShipmentDto MapToDto(IncomingShipment s, decimal? revenue, List<IncomingShipmentExpense> expenses)
    {
        var miscExpenses = expenses.Sum(e => e.Amount);
        var totalExpenses = s.OrderedAmount + miscExpenses;
        return new IncomingShipmentDto
        {
            Id = s.Id,
            Name = s.Name,
            WeightKg = s.WeightKg,
            ItemCount = s.ItemCount,
            OrderedAmount = s.OrderedAmount,
            Revenue = revenue,
            MiscExpensesTotal = miscExpenses,
            TotalExpenses = totalExpenses,
            ActualMargin = revenue.HasValue ? s.OrderedAmount - revenue.Value + miscExpenses : null,
            Notes = s.Notes,
            Expenses = expenses.Select(e => new IncomingShipmentExpenseDto
            {
                Id = e.Id,
                Name = e.Name,
                Amount = e.Amount,
                CreatedAt = e.CreatedAt
            }).ToList(),
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };
    }
}
