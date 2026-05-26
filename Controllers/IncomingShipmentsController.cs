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

        return Ok(shipments.Select(s => MapToDto(s, revenueMap.GetValueOrDefault(s.Id))).ToList());
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
        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, MapToDto(entity, null));
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

        var revenue = await (
            from oi in _context.OrderItems
            join o in _context.Orders on oi.OrderId equals o.Id
            join p in _context.Products on oi.ProductId equals p.Id
            where p.IncomingShipmentId == id && o.Status == "Получен"
            select (decimal?)(oi.ProductPrice * oi.Quantity)
        ).SumAsync();

        return Ok(MapToDto(entity, revenue));
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

    private static IncomingShipmentDto MapToDto(IncomingShipment s, decimal? revenue) => new()
    {
        Id = s.Id,
        Name = s.Name,
        WeightKg = s.WeightKg,
        ItemCount = s.ItemCount,
        OrderedAmount = s.OrderedAmount,
        Revenue = revenue,
        ActualMargin = revenue.HasValue ? revenue.Value - s.OrderedAmount : null,
        Notes = s.Notes,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}
