using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Controllers;

/// <summary>
/// Controller for managing shopping cart
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CartController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CartController> _logger;

    public CartController(AppDbContext context, ILogger<CartController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all cart items for a session
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CartItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CartItemDto>>> GetCartItems([FromQuery] string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return BadRequest(new { message = "SessionId is required" });

        // Очищаем устаревшие резервы (старше 30 минут)
        await CleanupExpiredReservationsAsync();

        var cartItems = await _context.CartItems
            .Include(c => c.Product)
            .Where(c => c.SessionId == sessionId)
            .Select(c => new CartItemDto
            {
                Id = c.Id,
                ProductId = c.ProductId,
                ProductName = c.Product!.Name,
                ProductPrice = c.Product.Price,
                ProductBrand = c.Product.Brand,
                ProductSize = c.Product.Size,
                ProductColor = c.Product.Color,
                ProductImages = c.Product.Images ?? new List<string>(),
                Quantity = c.Quantity,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(cartItems);
    }

    /// <summary>
    /// Adds a product to the cart (reserves it)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CartItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartItemDto>> AddToCart([FromBody] AddToCartDto dto)
    {
        if (string.IsNullOrEmpty(dto.SessionId))
            return BadRequest(new { message = "SessionId is required" });

        // Очищаем устаревшие резервы
        await CleanupExpiredReservationsAsync();

        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
            return NotFound(new { message = "Product not found" });

        // Вычисляем доступное количество (общее - зарезервированное в других корзинах)
        // Учитываем только активные резервы (не старше 30 минут)
        var expirationTime = DateTime.UtcNow.AddMinutes(-30);
        var reservedQuantity = await _context.CartItems
            .Where(c => c.ProductId == dto.ProductId && 
                       c.SessionId != dto.SessionId &&
                       c.UpdatedAt > expirationTime)
            .SumAsync(c => (int?)c.Quantity) ?? 0;

        var availableQuantity = product.QuantityInStock - reservedQuantity;

        if (availableQuantity <= 0)
            return BadRequest(new { message = "Product is out of stock" });

        var quantityToAdd = Math.Min(dto.Quantity, availableQuantity);
        if (quantityToAdd <= 0)
            return BadRequest(new { message = "Cannot add more items than available" });

        // Проверяем, есть ли уже этот товар в корзине
        var existingCartItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.SessionId == dto.SessionId && c.ProductId == dto.ProductId);

        if (existingCartItem != null)
        {
            // Проверяем, можем ли увеличить количество
            var newTotalQuantity = existingCartItem.Quantity + quantityToAdd;
            if (newTotalQuantity > availableQuantity)
            {
                return BadRequest(new { message = $"Only {availableQuantity} items available. You already have {existingCartItem.Quantity} in cart." });
            }

            existingCartItem.Quantity = newTotalQuantity;
            existingCartItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var cartItem = new CartItem
            {
                SessionId = dto.SessionId,
                ProductId = dto.ProductId,
                Quantity = quantityToAdd,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.CartItems.Add(cartItem);
            existingCartItem = cartItem;
        }

        await _context.SaveChangesAsync();

        // Загружаем продукт для ответа
        await _context.Entry(existingCartItem).Reference(c => c.Product).LoadAsync();

        var cartItemDto = new CartItemDto
        {
            Id = existingCartItem.Id,
            ProductId = existingCartItem.ProductId,
            ProductName = existingCartItem.Product!.Name,
            ProductPrice = existingCartItem.Product.Price,
            ProductBrand = existingCartItem.Product.Brand,
            ProductSize = existingCartItem.Product.Size,
            ProductColor = existingCartItem.Product.Color,
            ProductImages = existingCartItem.Product.Images ?? new List<string>(),
            Quantity = existingCartItem.Quantity,
            CreatedAt = existingCartItem.CreatedAt
        };

        return CreatedAtAction(nameof(GetCartItems), new { sessionId = dto.SessionId }, cartItemDto);
    }

    /// <summary>
    /// Updates the quantity of a cart item
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CartItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartItemDto>> UpdateCartItem(int id, [FromBody] UpdateCartItemDto dto)
    {
        if (dto.Quantity <= 0)
            return BadRequest(new { message = "Quantity must be greater than 0" });

        // Очищаем устаревшие резервы
        await CleanupExpiredReservationsAsync();

        var cartItem = await _context.CartItems
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cartItem == null)
            return NotFound();

        // Вычисляем доступное количество
        // Учитываем только активные резервы (не старше 30 минут)
        var expirationTime = DateTime.UtcNow.AddMinutes(-30);
        var reservedQuantity = await _context.CartItems
            .Where(c => c.ProductId == cartItem.ProductId && 
                       c.SessionId != cartItem.SessionId &&
                       c.UpdatedAt > expirationTime)
            .SumAsync(c => (int?)c.Quantity) ?? 0;

        var availableQuantity = cartItem.Product!.QuantityInStock - reservedQuantity;

        if (dto.Quantity > availableQuantity)
            return BadRequest(new { message = $"Only {availableQuantity} items available" });

        cartItem.Quantity = dto.Quantity;
        cartItem.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var cartItemDto = new CartItemDto
        {
            Id = cartItem.Id,
            ProductId = cartItem.ProductId,
            ProductName = cartItem.Product.Name,
            ProductPrice = cartItem.Product.Price,
            ProductBrand = cartItem.Product.Brand,
            ProductSize = cartItem.Product.Size,
            ProductColor = cartItem.Product.Color,
            ProductImages = cartItem.Product.Images ?? new List<string>(),
            Quantity = cartItem.Quantity,
            CreatedAt = cartItem.CreatedAt
        };

        return Ok(cartItemDto);
    }

    /// <summary>
    /// Removes an item from the cart (releases reservation)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromCart(int id)
    {
        var cartItem = await _context.CartItems.FindAsync(id);
        if (cartItem == null)
            return NotFound();

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Clears all cart items for a session
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearCart([FromQuery] string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return BadRequest(new { message = "SessionId is required" });

        var cartItems = await _context.CartItems
            .Where(c => c.SessionId == sessionId)
            .ToListAsync();

        _context.CartItems.RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Cleans up expired cart reservations (older than 30 minutes)
    /// </summary>
    private async Task CleanupExpiredReservationsAsync()
    {
        var expirationTime = DateTime.UtcNow.AddMinutes(-30);
        var expiredItems = await _context.CartItems
            .Where(c => c.UpdatedAt < expirationTime)
            .ToListAsync();

        if (expiredItems.Any())
        {
            _context.CartItems.RemoveRange(expiredItems);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Cleaned up {expiredItems.Count} expired cart reservations");
        }
    }
}

