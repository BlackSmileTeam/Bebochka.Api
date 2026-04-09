using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;
using Bebochka.Api.Services;
using Bebochka.Api.Helpers;

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
    private readonly WebReserveQueueService _queueService;

    public CartController(AppDbContext context, WebReserveQueueService queueService)
    {
        _context = context;
        _queueService = queueService;
    }

    private int? GetUserIdFromJwt()
    {
        var v = User.FindFirst("UserId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(v, out var id) ? id : null;
    }

    private static bool IsOwnCartLine(CartItem c, int? userId, string sessionId)
    {
        if (userId.HasValue)
            return c.UserId == userId;
        return c.UserId == null && c.SessionId == sessionId;
    }

    private static string SessionKeyForUser(int userId) => $"uid:{userId}";

    public class AdminCartItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductBrand { get; set; }
        public List<string> ProductImages { get; set; } = new();
        public int Quantity { get; set; }
        public int? UserId { get; set; }
        public string? SessionId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class QueueItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductBrand { get; set; }
        public List<string> ProductImages { get; set; } = new();
        public decimal ProductPrice { get; set; }
        public string? ProductSize { get; set; }
        public string? ProductColor { get; set; }
        public string? ProductCondition { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Gets all cart items for a session or logged-in user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CartItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CartItemDto>>> GetCartItems([FromQuery] string? sessionId)
    {
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue && string.IsNullOrEmpty(sessionId))
            return BadRequest(new { message = "SessionId is required for guests" });

        var query = _context.CartItems.Include(c => c.Product).AsQueryable();
        if (userId.HasValue)
            query = query.Where(c => c.UserId == userId.Value);
        else
            query = query.Where(c => c.SessionId == sessionId && c.UserId == null);

        var cartItems = await query
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
    /// Gets all active cart items for admin
    /// </summary>
    [HttpGet("admin/items")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(List<AdminCartItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AdminCartItemDto>>> GetAdminCartItems()
    {
        var items = await _context.CartItems
            .Include(c => c.Product)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new AdminCartItemDto
            {
                Id = c.Id,
                ProductId = c.ProductId,
                ProductName = c.Product != null ? c.Product.Name : "—",
                ProductBrand = c.Product != null ? c.Product.Brand : null,
                ProductImages = c.Product != null ? (c.Product.Images ?? new List<string>()) : new List<string>(),
                Quantity = c.Quantity,
                UserId = c.UserId,
                SessionId = c.SessionId,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Adds a product to the cart (reserves it)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CartItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartItemDto>> AddToCart([FromBody] AddToCartDto dto)
    {
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue && string.IsNullOrEmpty(dto.SessionId))
            return BadRequest(new { message = "SessionId is required for guests" });

        var strategy = _context.Database.CreateExecutionStrategy();
        CartItem? savedCartItem = null;
        ActionResult<CartItemDto>? earlyResult = null;

        await strategy.ExecuteAsync(async () =>
        {
            // ReadCommitted + блокировка строки товара: Serializable в InnoDB не гарантирует ту же сериализацию,
            // что и явный SELECT ... FOR UPDATE — без него два POST /cart могли оба увидеть «остаток есть».
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT `Id` FROM `Products` WHERE `Id` = {dto.ProductId} FOR UPDATE");

                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId);
                if (product == null)
                {
                    earlyResult = NotFound(new { message = "Product not found" });
                    await tx.RollbackAsync();
                    return;
                }

                var moscowNow = DateTimeHelper.GetMoscowTime();
                if (product.CartAvailableAt.HasValue && product.CartAvailableAt.Value > moscowNow)
                {
                    earlyResult = BadRequest(new { message = "Добавление в корзину будет доступно позже", cartLockedUntil = product.CartAvailableAt });
                    await tx.RollbackAsync();
                    return;
                }

                var reservedQuery = _context.CartItems
                    .Where(c => c.ProductId == dto.ProductId);
                if (userId.HasValue)
                    reservedQuery = reservedQuery.Where(c => c.UserId == null || c.UserId != userId.Value);
                else
                    reservedQuery = reservedQuery.Where(c => c.UserId != null || c.SessionId != dto.SessionId);
                var reservedQuantity = await reservedQuery.SumAsync(c => (int?)c.Quantity) ?? 0;

                var availableQuantity = product.QuantityInStock - reservedQuantity;
                if (availableQuantity <= 0)
                {
                    earlyResult = BadRequest(new { message = "Product is out of stock", code = "OUT_OF_STOCK" });
                    await tx.RollbackAsync();
                    return;
                }

                var quantityToAdd = Math.Min(dto.Quantity, availableQuantity);
                if (quantityToAdd <= 0)
                {
                    earlyResult = BadRequest(new { message = "Cannot add more items than available" });
                    await tx.RollbackAsync();
                    return;
                }

                var sessionKey = userId.HasValue ? SessionKeyForUser(userId.Value) : dto.SessionId;
                var existingCartItem = await _context.CartItems
                    .FirstOrDefaultAsync(c =>
                        c.ProductId == dto.ProductId &&
                        (userId.HasValue ? c.UserId == userId : c.SessionId == dto.SessionId && c.UserId == null));

                if (existingCartItem != null)
                {
                    var newTotalQuantity = existingCartItem.Quantity + quantityToAdd;
                    if (newTotalQuantity > availableQuantity)
                    {
                        earlyResult = BadRequest(new { message = $"Only {availableQuantity} items available. You already have {existingCartItem.Quantity} in cart." });
                        await tx.RollbackAsync();
                        return;
                    }
                    existingCartItem.Quantity = newTotalQuantity;
                    existingCartItem.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    existingCartItem = new CartItem
                    {
                        SessionId = sessionKey,
                        UserId = userId,
                        ProductId = dto.ProductId,
                        Quantity = quantityToAdd,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.CartItems.Add(existingCartItem);
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                savedCartItem = existingCartItem;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });

        if (earlyResult != null)
            return earlyResult;
        if (savedCartItem == null)
            return BadRequest(new { message = "Не удалось добавить товар в корзину" });

        await _context.Entry(savedCartItem).Reference(c => c.Product).LoadAsync();

        var cartItemDto = new CartItemDto
        {
            Id = savedCartItem.Id,
            ProductId = savedCartItem.ProductId,
            ProductName = savedCartItem.Product!.Name,
            ProductPrice = savedCartItem.Product.Price,
            ProductBrand = savedCartItem.Product.Brand,
            ProductSize = savedCartItem.Product.Size,
            ProductColor = savedCartItem.Product.Color,
            ProductImages = savedCartItem.Product.Images ?? new List<string>(),
            Quantity = savedCartItem.Quantity,
            CreatedAt = savedCartItem.CreatedAt
        };

        return CreatedAtAction(nameof(GetCartItems), new { sessionId = dto.SessionId }, cartItemDto);
    }

    /// <summary>
    /// В очередь на товар сайта (если нет свободного остатка из-за чужой корзины)
    /// </summary>
    [HttpPost("queue")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> JoinQueue([FromBody] JoinCartQueueDto dto)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null)
            return NotFound();

        var moscowNow = DateTimeHelper.GetMoscowTime();
        if (product.CartAvailableAt.HasValue && product.CartAvailableAt.Value > moscowNow)
            return BadRequest(new { message = "Очередь доступна после открытия корзины" });

        if (product.QuantityInStock <= 0)
            return BadRequest(new { message = "Товар не в наличии" });

        var reservedQuery = _context.CartItems
            .Where(c => c.ProductId == dto.ProductId);
        reservedQuery = reservedQuery.Where(c => c.UserId == null || c.UserId != userId);
        var reservedQuantity = await reservedQuery.SumAsync(c => (int?)c.Quantity) ?? 0;

        var availableQuantity = product.QuantityInStock - reservedQuantity;
        if (availableQuantity > 0)
            return BadRequest(new { message = "Товар доступен — добавьте в корзину" });

        var alreadyInCart = await _context.CartItems.AnyAsync(c =>
            c.ProductId == dto.ProductId && c.UserId == userId);
        if (alreadyInCart)
            return BadRequest(new { message = "Уже в корзине" });

        var inQueue = await _context.ReserveQueue.AnyAsync(r =>
            r.ProductId == dto.ProductId && r.WebUserId == userId);
        if (inQueue)
            return NoContent();

        _context.ReserveQueue.Add(new ReserveQueue
        {
            ProductId = dto.ProductId,
            ChannelId = "web",
            PostMessageId = 0,
            TelegramUserId = null,
            WebUserId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Gets current user's web queue items
    /// </summary>
    [HttpGet("queue/mine")]
    [Authorize]
    [ProducesResponseType(typeof(List<QueueItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<QueueItemDto>>> GetMyQueue()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var items = await _context.ReserveQueue
            .Include(r => r.Product)
            .Where(r => r.WebUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new QueueItemDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                ProductName = r.Product != null ? r.Product.Name : "—",
                ProductBrand = r.Product != null ? r.Product.Brand : null,
                ProductImages = r.Product != null ? (r.Product.Images ?? new List<string>()) : new List<string>(),
                ProductPrice = r.Product != null ? r.Product.Price : 0,
                ProductSize = r.Product != null ? r.Product.Size : null,
                ProductColor = r.Product != null ? r.Product.Color : null,
                ProductCondition = r.Product != null ? r.Product.Condition : null,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Cancels current user's queue item by queue id
    /// </summary>
    [HttpDelete("queue/{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelMyQueueItem(int id)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var item = await _context.ReserveQueue.FirstOrDefaultAsync(r => r.Id == id && r.WebUserId == userId);
        if (item == null)
            return NotFound();

        _context.ReserveQueue.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
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

        var cartItem = await _context.CartItems
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cartItem == null)
            return NotFound();

        var userId = GetUserIdFromJwt();
        if (userId.HasValue)
        {
            if (cartItem.UserId != userId)
                return Forbid();
        }
        else if (cartItem.UserId != null)
            return Forbid();

        var reservedQuantity = await _context.CartItems
            .Where(c => c.ProductId == cartItem.ProductId && c.Id != cartItem.Id)
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

        var userId = GetUserIdFromJwt();
        if (userId.HasValue)
        {
            if (cartItem.UserId != userId)
                return Forbid();
        }
        else if (cartItem.UserId != null)
            return Forbid();

        var productId = cartItem.ProductId;
        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();

        await _queueService.PromoteNextAfterCartReleaseAsync(productId);

        return NoContent();
    }

    /// <summary>
    /// Removes cart item by id (admin only)
    /// </summary>
    [HttpDelete("admin/items/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminRemoveFromCart(int id)
    {
        var cartItem = await _context.CartItems.FindAsync(id);
        if (cartItem == null)
            return NotFound();

        var productId = cartItem.ProductId;
        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();

        await _queueService.PromoteNextAfterCartReleaseAsync(productId);
        return NoContent();
    }

    /// <summary>
    /// Clears all cart items for a session or user
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearCart([FromQuery] string? sessionId)
    {
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue && string.IsNullOrEmpty(sessionId))
            return BadRequest(new { message = "SessionId is required for guests" });

        List<CartItem> cartItems;
        if (userId.HasValue)
        {
            cartItems = await _context.CartItems
                .Where(c => c.UserId == userId.Value)
                .ToListAsync();
        }
        else
        {
            cartItems = await _context.CartItems
                .Where(c => c.SessionId == sessionId && c.UserId == null)
                .ToListAsync();
        }

        var productIds = cartItems.Select(c => c.ProductId).Distinct().ToList();
        _context.CartItems.RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        foreach (var pid in productIds)
            await _queueService.PromoteNextAfterCartReleaseAsync(pid);

        return NoContent();
    }
}
