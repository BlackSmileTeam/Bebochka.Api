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
    private readonly IProductKitService _kitService;

    public CartController(AppDbContext context, WebReserveQueueService queueService, IProductKitService kitService)
    {
        _context = context;
        _queueService = queueService;
        _kitService = kitService;
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

    private async Task<bool> IsAdminUserAsync(int? userId)
    {
        if (!userId.HasValue)
            return false;
        return await _context.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId.Value && u.IsAdmin);
    }

    private static bool IsTestProductHiddenFromUser(Product? product, bool isAdmin) =>
        product != null && product.IsTestProduct && !isAdmin;

    /// <summary>Скрытые строки брони частей комплекта (в корзине виден только display-товар).</summary>
    private static bool IsKitBundlePartLine(CartItem c) =>
        c.CartAddMode == ProductKitService.CartAddModeBundle
        && c.Product != null
        && !c.Product.IsKitDisplay;

    private static IQueryable<CartItem> FilterOwnCartLines(IQueryable<CartItem> query, int? userId, string? sessionId)
    {
        if (userId.HasValue)
            return query.Where(c => c.UserId == userId.Value);
        return query.Where(c => c.SessionId == sessionId && c.UserId == null);
    }

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
        public string? CustomerName { get; set; }
        public long? VkUserId { get; set; }
        public string? VkProfileUrl { get; set; }
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

        var isAdmin = await IsAdminUserAsync(userId);
        var query = _context.CartItems.Include(c => c.Product).AsQueryable();
        if (userId.HasValue)
            query = query.Where(c => c.UserId == userId.Value);
        else
            query = query.Where(c => c.SessionId == sessionId && c.UserId == null);

        var cartItems = await query.ToListAsync();
        if (!isAdmin)
            cartItems = cartItems.Where(c => c.Product == null || !c.Product.IsTestProduct).ToList();
        cartItems = cartItems.Where(c => !IsKitBundlePartLine(c)).ToList();
        var dtos = cartItems.Select(c => new CartItemDto
        {
            Id = c.Id,
            ProductId = c.ProductId,
            ProductName = c.Product!.Name,
            ProductPrice = c.ChargedUnitPrice ?? c.Product.Price,
            ProductBrand = c.Product.Brand,
            ProductSize = c.Product.Size,
            ProductColor = c.Product.Color,
            ProductImages = c.Product.Images ?? new List<string>(),
            Quantity = c.Quantity,
            CreatedAt = c.CreatedAt,
            KitId = c.KitId,
            CartAddMode = c.CartAddMode,
            KitBundleKey = c.KitBundleKey,
            KitPartName = c.Product.KitPartName,
            IsKitDisplayLine = c.Product.IsKitDisplay,
        }).ToList();

        return Ok(dtos);
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
            .Include(c => c.User)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

        var result = items.Select(c =>
        {
            var vkUserId = c.User?.VkUserId;
            return new AdminCartItemDto
            {
                Id = c.Id,
                ProductId = c.ProductId,
                ProductName = c.Product?.Name ?? "—",
                ProductBrand = c.Product?.Brand,
                ProductImages = c.Product?.Images ?? new List<string>(),
                Quantity = c.Quantity,
                UserId = c.UserId,
                SessionId = c.SessionId,
                UpdatedAt = c.UpdatedAt,
                CustomerName = c.User != null
                    ? (c.User.FullName ?? c.User.Username)
                    : null,
                VkUserId = vkUserId,
                VkProfileUrl = vkUserId is > 0 ? $"https://vk.com/id{vkUserId.Value}" : null
            };
        }).ToList();

        return Ok(result);
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

        var isAdmin = await IsAdminUserAsync(userId);
        var addMode = (dto.AddMode ?? string.Empty).Trim().ToLowerInvariant();
        if (addMode == ProductKitService.CartAddModeBundle)
            return await AddKitBundleToCartAsync(dto, userId);

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
                    $"SELECT `Id` FROM `products` WHERE `Id` = {dto.ProductId} FOR UPDATE");

                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId);
                if (product == null)
                {
                    earlyResult = NotFound(new { message = "Product not found" });
                    await tx.RollbackAsync();
                    return;
                }

                if (IsTestProductHiddenFromUser(product, isAdmin))
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
                        KitId = product.KitId,
                        CartAddMode = product.KitId.HasValue ? ProductKitService.CartAddModePart : null,
                        ChargedUnitPrice = product.Price,
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
            ProductPrice = savedCartItem.ChargedUnitPrice ?? savedCartItem.Product.Price,
            ProductBrand = savedCartItem.Product.Brand,
            ProductSize = savedCartItem.Product.Size,
            ProductColor = savedCartItem.Product.Color,
            ProductImages = savedCartItem.Product.Images ?? new List<string>(),
            Quantity = savedCartItem.Quantity,
            CreatedAt = savedCartItem.CreatedAt,
            KitId = savedCartItem.KitId,
            CartAddMode = savedCartItem.CartAddMode,
            KitBundleKey = savedCartItem.KitBundleKey,
            KitPartName = savedCartItem.Product.KitPartName,
            IsKitDisplayLine = savedCartItem.Product.IsKitDisplay,
        };

        return CreatedAtAction(nameof(GetCartItems), new { sessionId = dto.SessionId }, cartItemDto);
    }

    private async Task<ActionResult<CartItemDto>> AddKitBundleToCartAsync(AddToCartDto dto, int? userId)
    {
        var isAdmin = await IsAdminUserAsync(userId);
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product?.KitId == null)
            return BadRequest(new { message = "Товар не является комплектом", code = "NOT_A_KIT" });

        if (IsTestProductHiddenFromUser(product, isAdmin))
            return NotFound(new { message = "Product not found" });

        var kitId = product.KitId.Value;
        var kit = await _context.ProductKits.FindAsync(kitId);
        if (kit == null)
            return NotFound(new { message = "Комплект не найден" });

        var kitProducts = await _context.Products
            .Where(p => p.KitId == kitId)
            .OrderBy(p => p.IsKitDisplay ? 0 : 1)
            .ThenBy(p => p.KitPartSortOrder)
            .ToListAsync();

        if (kitProducts.Count == 0)
            return BadRequest(new { message = "Состав комплекта пуст" });

        var moscowNow = DateTimeHelper.GetMoscowTime();
        var display = kitProducts.FirstOrDefault(p => p.IsKitDisplay) ?? product;
        if (display.CartAvailableAt.HasValue && display.CartAvailableAt.Value > moscowNow)
            return BadRequest(new { message = "Добавление в корзину будет доступно позже", cartLockedUntil = display.CartAvailableAt });

        var kitProductIds = kitProducts.Select(p => p.Id).ToList();
        var options = await _kitService.GetKitOptionsAsync(display.Id, dto.SessionId, userId);
        if (options == null || !options.CanAddFullKit)
            return BadRequest(new { message = "Не все вещи комплекта доступны", code = "KIT_PART_RESERVED" });

        var sessionKey = userId.HasValue ? SessionKeyForUser(userId.Value) : dto.SessionId;
        var bundleKey = Guid.NewGuid().ToString("N");
        CartItem? firstLine = null;

        try
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                try
                {
                    foreach (var pid in kitProductIds)
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"SELECT `Id` FROM `products` WHERE `Id` = {pid} FOR UPDATE");
                    }

                    var existingForKit = await _context.CartItems
                        .Where(c => kitProductIds.Contains(c.ProductId))
                        .Where(c => userId.HasValue ? c.UserId == userId : c.SessionId == dto.SessionId && c.UserId == null)
                        .ToListAsync();

                    if (existingForKit.Any(c =>
                            c.ProductId == display.Id
                            && c.CartAddMode == ProductKitService.CartAddModeBundle))
                    {
                        throw new InvalidOperationException("KIT_ALREADY_IN_CART");
                    }

                    if (existingForKit.Count > 0)
                        _context.CartItems.RemoveRange(existingForKit);

                    foreach (var kp in kitProducts)
                    {
                        var reservedQuery = _context.CartItems.Where(c => c.ProductId == kp.Id);
                        if (userId.HasValue)
                            reservedQuery = reservedQuery.Where(c => c.UserId == null || c.UserId != userId.Value);
                        else
                            reservedQuery = reservedQuery.Where(c => c.UserId != null || c.SessionId != dto.SessionId);
                        var reserved = await reservedQuery.SumAsync(c => (int?)c.Quantity) ?? 0;
                        if (kp.QuantityInStock - reserved <= 0)
                            throw new InvalidOperationException("KIT_PART_RESERVED");

                        var line = new CartItem
                        {
                            SessionId = sessionKey,
                            UserId = userId,
                            ProductId = kp.Id,
                            Quantity = 1,
                            KitId = kitId,
                            CartAddMode = ProductKitService.CartAddModeBundle,
                            KitBundleKey = bundleKey,
                            ChargedUnitPrice = kp.IsKitDisplay ? kit.KitPrice : 0m,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                        };
                        _context.CartItems.Add(line);
                        if (kp.IsKitDisplay)
                            firstLine = line;
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });
        }
        catch (InvalidOperationException ex) when (ex.Message == "KIT_PART_RESERVED")
        {
            return BadRequest(new { message = "Не все вещи комплекта доступны", code = "KIT_PART_RESERVED" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "KIT_ALREADY_IN_CART")
        {
            return BadRequest(new { message = "Комплект уже в корзине", code = "KIT_ALREADY_IN_CART" });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "Не все вещи комплекта доступны", code = "KIT_PART_RESERVED" });
        }

        if (firstLine == null)
        {
            firstLine = await _context.CartItems
                .Include(c => c.Product)
                .FirstOrDefaultAsync(c =>
                    c.ProductId == display.Id
                    && c.CartAddMode == ProductKitService.CartAddModeBundle
                    && (userId.HasValue ? c.UserId == userId : c.SessionId == dto.SessionId && c.UserId == null));
            if (firstLine == null)
                return BadRequest(new { message = "Комплект уже в корзине или недоступен", code = "KIT_UNAVAILABLE" });
        }
        else
        {
            await _context.Entry(firstLine).Reference(c => c.Product).LoadAsync();
        }

        var cartItemDto = new CartItemDto
        {
            Id = firstLine.Id,
            ProductId = display.Id,
            ProductName = display.Name,
            ProductPrice = kit.KitPrice,
            ProductBrand = display.Brand,
            ProductSize = display.Size,
            ProductColor = display.Color,
            ProductImages = display.Images ?? new List<string>(),
            Quantity = 1,
            CreatedAt = firstLine.CreatedAt,
            KitId = kitId,
            CartAddMode = ProductKitService.CartAddModeBundle,
            KitBundleKey = bundleKey,
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

        if (IsTestProductHiddenFromUser(product, await IsAdminUserAsync(userId)))
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

        var linesToRemove = new List<CartItem> { cartItem };
        if (!string.IsNullOrEmpty(cartItem.KitBundleKey)
            && cartItem.CartAddMode == ProductKitService.CartAddModeBundle)
        {
            var bundleQuery = _context.CartItems.Where(c => c.KitBundleKey == cartItem.KitBundleKey);
            bundleQuery = FilterOwnCartLines(bundleQuery, userId, cartItem.SessionId);
            linesToRemove = await bundleQuery.ToListAsync();
        }

        var productIds = linesToRemove.Select(c => c.ProductId).Distinct().ToList();
        _context.CartItems.RemoveRange(linesToRemove);
        await _context.SaveChangesAsync();

        foreach (var pid in productIds)
            await _queueService.PromoteNextAfterCartReleaseAsync(pid);

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

        var linesToRemove = new List<CartItem> { cartItem };
        if (!string.IsNullOrEmpty(cartItem.KitBundleKey)
            && cartItem.CartAddMode == ProductKitService.CartAddModeBundle)
        {
            linesToRemove = await _context.CartItems
                .Where(c => c.KitBundleKey == cartItem.KitBundleKey)
                .ToListAsync();
        }

        var productIds = linesToRemove.Select(c => c.ProductId).Distinct().ToList();
        _context.CartItems.RemoveRange(linesToRemove);
        await _context.SaveChangesAsync();

        foreach (var pid in productIds)
            await _queueService.PromoteNextAfterCartReleaseAsync(pid);
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
