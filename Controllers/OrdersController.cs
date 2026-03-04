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
    /// Deletes an order and its items from the database (admin only).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var deleted = await _orderService.DeleteOrderAsync(id);
        if (!deleted)
            return NotFound();
        return NoContent();
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

    /// <summary>
    /// Reserves a product from a Telegram channel post (first allowed comment).
    /// </summary>
    [HttpPost("reserve-from-telegram")]
    [ProducesResponseType(typeof(ReserveFromTelegramResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReserveFromTelegramResultDto>> ReserveFromTelegram([FromBody] ReserveFromTelegramRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ChannelId))
            return BadRequest(new ReserveFromTelegramResultDto { Success = false, Reason = "ChannelId required" });
        if (dto.MessageId <= 0)
            return BadRequest(new ReserveFromTelegramResultDto { Success = false, Reason = "MessageId required" });
        if (dto.TelegramUserId <= 0)
            return BadRequest(new ReserveFromTelegramResultDto { Success = false, Reason = "TelegramUserId required" });

        var result = await _orderService.ReserveFromTelegramAsync(
            dto.ChannelId,
            dto.MessageId,
            dto.TelegramUserId,
            dto.Username,
            dto.FirstName,
            dto.LastName,
            dto.CustomerPhone,
            dto.CommentChatId,
            dto.CommentMessageId);
        return Ok(result);
    }

    /// <summary>
    /// Removes an item from an order. Deletes user's Telegram comment, restores stock, optionally assigns product to next user from reserve queue.
    /// </summary>
    [HttpDelete("{orderId}/items/{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteOrderItem(int orderId, int itemId)
    {
        var ok = await _orderService.DeleteOrderItemAsync(orderId, itemId);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Marks an order item as added to parcel or not.
    /// </summary>
    [HttpPatch("{orderId}/items/{itemId}/in-parcel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetOrderItemInParcel(int orderId, int itemId, [FromBody] SetOrderItemInParcelDto dto)
    {
        var ok = await _orderService.SetOrderItemAddedToParcelAsync(orderId, itemId, dto.AddedToParcel);
        if (!ok) return NotFound();
        return Ok();
    }

    /// <summary>
    /// Applies discount to selected orders (bulk).
    /// </summary>
    [HttpPost("apply-discount")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ApplyDiscount([FromBody] ApplyDiscountDto dto)
    {
        if (dto.OrderIds == null || dto.OrderIds.Count == 0)
            return BadRequest(new { message = "OrderIds required" });
        if (dto.DiscountType != "Fixed" && dto.DiscountType != "ByCondition")
            return BadRequest(new { message = "DiscountType must be Fixed or ByCondition" });
        if (dto.DiscountType == "Fixed" && (!dto.FixedDiscountPercent.HasValue || dto.FixedDiscountPercent.Value < 0 || dto.FixedDiscountPercent.Value > 100))
            return BadRequest(new { message = "FixedDiscountPercent must be 0-100" });
        await _orderService.ApplyDiscountToOrdersAsync(dto.OrderIds, dto.DiscountType, dto.FixedDiscountPercent, dto.Condition1ItemPercent, dto.Condition3ItemsPercent, dto.Condition5PlusPercent);
        return Ok();
    }

    /// <summary>
    /// Removes discount from an order.
    /// </summary>
    [HttpDelete("{orderId}/discount")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveOrderDiscount(int orderId)
    {
        var ok = await _orderService.RemoveOrderDiscountAsync(orderId);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Applies fixed discount (percent) to a single order.
    /// </summary>
    [HttpPut("{orderId}/discount")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetOrderDiscount(int orderId, [FromBody] SetOrderDiscountDto dto)
    {
        if (dto.Percent < 0 || dto.Percent > 100)
            return BadRequest(new { message = "Percent must be 0-100" });
        var ok = await _orderService.ApplyOrderDiscountAsync(orderId, dto.Percent);
        if (!ok) return NotFound();
        return Ok();
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

public class SetOrderItemInParcelDto
{
    public bool AddedToParcel { get; set; }
}

public class ApplyDiscountDto
{
    public List<int> OrderIds { get; set; } = new();
    public string DiscountType { get; set; } = "Fixed";
    public int? FixedDiscountPercent { get; set; }
    public int? Condition1ItemPercent { get; set; }
    public int? Condition3ItemsPercent { get; set; }
    public int? Condition5PlusPercent { get; set; }
}

public class SetOrderDiscountDto
{
    public int Percent { get; set; }
}

