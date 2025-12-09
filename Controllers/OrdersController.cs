using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;

namespace Bebochka.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Creates a new order
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto dto)
    {
        try
        {
            var order = await _orderService.CreateOrderAsync(dto);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all orders (admin only)
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrderDto>>> GetAllOrders()
    {
        var orders = await _orderService.GetAllOrdersAsync();
        return Ok(orders);
    }

    /// <summary>
    /// Gets all orders (for bot, no auth required)
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrderDto>>> GetAllOrdersPublic([FromQuery] string? status = null)
    {
        var orders = await _orderService.GetAllOrdersAsync();
        if (!string.IsNullOrEmpty(status))
        {
            orders = orders.Where(o => o.Status == status).ToList();
        }
        return Ok(orders);
    }

    /// <summary>
    /// Gets orders by user ID
    /// </summary>
    [HttpGet("user")]
    [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrderDto>>> GetUserOrders([FromQuery] int userId)
    {
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return Ok(orders);
    }

    /// <summary>
    /// Gets an order by ID (admin only)
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }

    /// <summary>
    /// Gets an order by ID (public, for bot)
    /// </summary>
    [HttpGet("{id}/public")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrderPublic(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }

    /// <summary>
    /// Cancels an order
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CancelOrder(int id, [FromBody] CancelOrderDto? dto = null)
    {
        var success = await _orderService.CancelOrderAsync(id, dto?.Reason);
        if (!success)
            return BadRequest(new { message = "Не удалось отменить заказ" });

        return Ok(new { message = "Заказ отменен" });
    }

    /// <summary>
    /// Updates order status (admin)
    /// </summary>
    [HttpPut("{id}/status")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        var success = await _orderService.UpdateOrderStatusAsync(id, dto.Status);
        if (!success)
            return BadRequest(new { message = "Не удалось обновить статус заказа" });

        return Ok(new { message = "Статус обновлен" });
    }

    /// <summary>
    /// Updates order status (public, for bot)
    /// </summary>
    [HttpPut("{id}/status/public")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateOrderStatusPublic(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        var success = await _orderService.UpdateOrderStatusAsync(id, dto.Status);
        if (!success)
            return BadRequest(new { message = "Не удалось обновить статус заказа" });

        return Ok(new { message = "Статус обновлен" });
    }

    /// <summary>
    /// Gets order statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(OrderStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderStatisticsDto>> GetStatistics()
    {
        var statistics = await _orderService.GetStatisticsAsync();
        return Ok(statistics);
    }
}

public class CancelOrderDto
{
    public string? Reason { get; set; }
}

public class UpdateOrderStatusDto
{
    public string Status { get; set; } = string.Empty;
}

