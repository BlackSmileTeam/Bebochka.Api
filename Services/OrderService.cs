using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Helpers;
using Bebochka.Api.Models;
using Bebochka.Api.Models.DTOs;

namespace Bebochka.Api.Services;

/// <summary>
/// Service implementation for order operations
/// </summary>
public class OrderService : IOrderService
{
    /// <summary>Статус «Получен» выставляется только клиентом с сайта, не через админку.</summary>
    public const string StatusReceived = "Получен";

    /// <summary>Автоматический статус родителя при частичной отправке (не выбирается вручную).</summary>
    public const string StatusPartiallyShipped = "Отправлено частично";

    private static readonly string[] AdminSelectableStatuses =
    {
        "Формирование заказа", "Ожидает оплату", "Копим", "Оплачен", "В сборке", "На доставку", "Отправлен", "Отменен"
    };

    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly WebReserveQueueService _queueService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;
    private readonly IReferralService _referralService;

    public OrderService(
        AppDbContext context,
        IEmailService emailService,
        WebReserveQueueService queueService,
        IHttpContextAccessor httpContextAccessor,
        IWebHostEnvironment environment,
        IReferralService referralService)
    {
        _context = context;
        _emailService = emailService;
        _queueService = queueService;
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
        _referralService = referralService;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            throw new InvalidOperationException("В заказе должна быть хотя бы одна позиция.");

        // Генерируем номер заказа
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        
        // Получаем товары и проверяем наличие
        var productIds = dto.Items.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var orderItems = new List<OrderItem>();
        var totalAmount = 0m;

        foreach (var itemDto in dto.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == itemDto.ProductId);
            if (product == null)
                throw new InvalidOperationException($"Product {itemDto.ProductId} not found");

            if (product.QuantityInStock < itemDto.Quantity)
                throw new InvalidOperationException($"Not enough stock for product {product.Name}. Available: {product.QuantityInStock}, Requested: {itemDto.Quantity}");

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductPrice = product.Price,
                Quantity = itemDto.Quantity
            };

            orderItems.Add(orderItem);
            totalAmount += product.Price * itemDto.Quantity;

            // Уменьшаем количество товара на складе
            product.QuantityInStock -= itemDto.Quantity;
        }

        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = dto.UserId,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            CustomerEmail = dto.CustomerEmail,
            CustomerAddress = dto.CustomerAddress,
            DeliveryMethod = dto.DeliveryMethod,
            Comment = dto.Comment,
            TotalAmount = totalAmount,
            Status = "Ожидает оплату",
            OrderItems = orderItems,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        order.StatusHistories.Add(new OrderStatusHistory
        {
            Status = order.Status,
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = dto.UserId
        });

        // Удаляем товары из корзины пользователя (гость по SessionId, авторизованный — по UserId)
        List<CartItem> cartItems;
        if (dto.UserId.HasValue)
        {
            cartItems = await _context.CartItems
                .Where(c => c.UserId == dto.UserId.Value)
                .ToListAsync();
        }
        else
        {
            cartItems = await _context.CartItems
                .Where(c => c.SessionId == dto.SessionId && c.UserId == null)
                .ToListAsync();
        }
        _context.CartItems.RemoveRange(cartItems);

        await RemoveReserveQueueForProductsAsync(productIds);

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        if (dto.UserId.HasValue && !string.IsNullOrWhiteSpace(dto.ReferralDiscountKind))
        {
            var kind = dto.ReferralDiscountKind.Trim();
            var referralId = await _referralService.ResolveReferralIdForCheckoutAsync(
                dto.UserId.Value,
                kind,
                dto.ReferralDiscountReferralId);

            if (referralId.HasValue)
            {
                await _referralService.ApplyReferralDiscountToOrderAsync(
                    dto.UserId.Value,
                    order.Id,
                    referralId.Value,
                    kind,
                    totalAmount);
            }
        }

        // Отправляем email
        try
        {
            await _emailService.SendOrderNotificationAsync(order);
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не прерываем создание заказа
            Console.WriteLine($"Failed to send order email: {ex.Message}");
        }

        User? user = null;
        if (order.UserId.HasValue)
            user = await _context.Users.FirstOrDefaultAsync(u => u.Id == order.UserId.Value);
        var hasReview = await _context.OrderCustomerReviews.AnyAsync(r => r.OrderId == order.Id);
        return MapToDto(order, user, hasReview);
    }

    public async Task<List<OrderDto>> GetAllOrdersAsync()
    {
        List<Order> orders;
        try
        {
            orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.StatusHistories)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("Unknown column 'o0.CreatedAt'", StringComparison.OrdinalIgnoreCase))
        {
            // Compatibility fallback for databases that were not migrated with OrderItems.CreatedAt yet.
            orders = await _context.Orders
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        // Загружаем информацию о пользователях для заказов
        var userIds = orders.Where(o => o.UserId.HasValue).Select(o => o.UserId!.Value).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u);

        var reviewedOrderIds = (await _context.OrderCustomerReviews
            .Where(r => r.OrderId.HasValue)
            .Select(r => r.OrderId!.Value)
            .ToListAsync()).ToHashSet();

        var rootOrders = orders.Where(o => o.ParentOrderId == null).OrderByDescending(o => o.CreatedAt).ToList();
        var childrenByParent = orders
            .Where(o => o.ParentOrderId.HasValue)
            .GroupBy(o => o.ParentOrderId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.OrderNumber).ToList());

        return rootOrders.Select(o =>
        {
            var dto = MapToDto(o, users.GetValueOrDefault(o.UserId ?? 0), reviewedOrderIds.Contains(o.Id));
            if (childrenByParent.TryGetValue(o.Id, out var children))
            {
                dto.ChildOrders = children
                    .Select(c => MapToDto(c, users.GetValueOrDefault(c.UserId ?? 0), reviewedOrderIds.Contains(c.Id)))
                    .ToList();
            }
            return dto;
        }).ToList();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return null;

        User? user = null;
        if (order.UserId.HasValue)
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Id == order.UserId.Value);
        }

        var hasReview = await _context.OrderCustomerReviews.AnyAsync(r => r.OrderId == id);
        return MapToDto(order, user, hasReview);
    }

    public async Task<List<OrderDto>> GetUserOrdersAsync(int userId)
    {
        List<Order> orders;
        try
        {
            orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.StatusHistories)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("Unknown column 'o0.CreatedAt'", StringComparison.OrdinalIgnoreCase))
        {
            // Compatibility fallback for databases that were not migrated with OrderItems.CreatedAt yet.
            orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        var userOrderIds = orders.Select(o => o.Id).ToHashSet();
        var reviewedOrderIds = (await _context.OrderCustomerReviews
            .Where(r => r.OrderId.HasValue && userOrderIds.Contains(r.OrderId.Value))
            .Select(r => r.OrderId!.Value)
            .ToListAsync()).ToHashSet();

        var rootOrders = orders.Where(o => o.ParentOrderId == null).OrderByDescending(o => o.CreatedAt).ToList();
        var childrenByParent = orders
            .Where(o => o.ParentOrderId.HasValue)
            .GroupBy(o => o.ParentOrderId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.OrderNumber).ToList());

        return rootOrders.Select(o =>
        {
            var dto = MapToDto(o, user, reviewedOrderIds.Contains(o.Id));
            if (childrenByParent.TryGetValue(o.Id, out var children))
            {
                dto.ChildOrders = children
                    .Select(c => MapToDto(c, user, reviewedOrderIds.Contains(c.Id)))
                    .ToList();
            }
            return dto;
        }).ToList();
    }

    public async Task<bool> CancelOrderAsync(int orderId, string? reason = null)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return false;

        if (order.Status == "Отправлен" || order.Status == StatusReceived || order.Status == "Отменен")
            return false;

        // Return products to stock
        foreach (var item in order.OrderItems)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.QuantityInStock += item.Quantity;
            }
        }

        order.Status = "Отменен";
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = reason;
        order.UpdatedAt = DateTime.UtcNow;

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = "Отменен",
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = GetActorUserIdFromHttp()
        });

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>Приводит строку статуса к канону (пробелы, старые названия из БД).</summary>
    private static string NormalizeIncomingOrderStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim();
        return s switch
        {
            "В пути" => "На доставку",
            "Доставлен" => "Отправлен",
            _ => s
        };
    }

    private static string NormalizeOrderStatusForApi(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return NormalizeIncomingOrderStatus(raw);
    }

    public async Task<OrderStatusUpdateOutcome> UpdateOrderStatusAsync(int orderId, string statusRaw, bool confirmSplit = false)
    {
        var status = NormalizeIncomingOrderStatus(statusRaw);
        if (string.IsNullOrWhiteSpace(status))
            return new OrderStatusUpdateOutcome(false, "Не указан новый статус (пустое значение в запросе).");

        if (status == StatusReceived)
            return new OrderStatusUpdateOutcome(false, "Статус «Получен» может установить только клиент на сайте.");

        if (status == StatusPartiallyShipped)
            return new OrderStatusUpdateOutcome(false, "Статус «Отправлено частично» выставляется автоматически при частичной отправке.");

        if (!AdminSelectableStatuses.Contains(status))
            return new OrderStatusUpdateOutcome(false,
                $"Статус «{status}» недоступен для смены. Допустимые: {string.Join(", ", AdminSelectableStatuses)}.");

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
            return new OrderStatusUpdateOutcome(false, "Заказ не найден.");

        var previousStatus = order.Status?.Trim() ?? string.Empty;

        if (string.Equals(previousStatus, StatusReceived, StringComparison.Ordinal))
            return new OrderStatusUpdateOutcome(false, "Заказ уже в статусе «Получен» — изменение статуса запрещено.");

        if (string.Equals(previousStatus, "Отменен", StringComparison.Ordinal))
            return new OrderStatusUpdateOutcome(false, "Заказ отменён — изменение статуса запрещено.");

        if (string.Equals(previousStatus, StatusPartiallyShipped, StringComparison.Ordinal))
            return new OrderStatusUpdateOutcome(false, "Статус родительского заказа меняется автоматически после отправки всех частей.");

        if (status == "Отправлен"
            && (string.Equals(previousStatus, "В сборке", StringComparison.Ordinal)
                || string.Equals(previousStatus, "На доставку", StringComparison.Ordinal))
            && order.ParentOrderId == null
            && !await _context.Orders.AnyAsync(o => o.ParentOrderId == orderId))
        {
            var inParcel = order.OrderItems.Where(i => i.AddedToParcel).ToList();
            var notInParcel = order.OrderItems.Where(i => !i.AddedToParcel).ToList();
            if (inParcel.Count > 0 && notInParcel.Count > 0)
            {
                if (!confirmSplit)
                    return new OrderStatusUpdateOutcome(false,
                        "Не все позиции отмечены «В посылке». Подтвердите разбиение заказа на две отправки.",
                        RequiresSplitConfirmation: true);
                return await SplitAndShipPartialOrderAsync(order, inParcel, notInParcel);
            }
        }

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        if (status == "Отменен")
        {
            if (order.CancelledAt == null)
                order.CancelledAt = DateTime.UtcNow;

            // Возврат товаров в каталог (как при CancelOrderAsync).
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product != null)
                    product.QuantityInStock += item.Quantity;
            }

            var cancelledProductIds = order.OrderItems.Select(oi => oi.ProductId).Distinct().ToList();
            await RemoveReserveQueueForProductsAsync(cancelledProductIds);
        }

        if (status == "Отправлен")
        {
            var shippedProductIds = await _context.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .Select(oi => oi.ProductId)
                .Distinct()
                .ToListAsync();
            await RemoveReserveQueueForProductsAsync(shippedProductIds);
        }

        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId,
            Status = status,
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = GetActorUserIdFromHttp()
        });

        await _context.SaveChangesAsync();

        if (status == "Отправлен" && order.ParentOrderId.HasValue)
            await TryPromoteParentWhenAllChildrenShippedAsync(order.ParentOrderId.Value);

        return new OrderStatusUpdateOutcome(true);
    }

    public async Task<OrderStatisticsDto> GetStatisticsAsync()
    {
        var orders = await _context.Orders.ToListAsync();

        return new OrderStatisticsDto
        {
            TotalOrders = orders.Count,
            FormingOrders = orders.Count(o => o.Status == "Формирование заказа"),
            AwaitingPaymentOrders = orders.Count(o => o.Status == "Ожидает оплату"),
            CollectingOrders = orders.Count(o => o.Status == "Копим"),
            PendingOrders = orders.Count(o => o.Status == "В сборке"),
            OnDeliveryOrders = orders.Count(o => o.Status == "На доставку"),
            SentOrders = orders.Count(o => o.Status == "Отправлен"),
            ReceivedOrders = orders.Count(o => o.Status == StatusReceived),
            CancelledOrders = orders.Count(o => o.Status == "Отменен"),
            TotalRevenue = orders.Where(o => o.Status == "Отправлен" || o.Status == StatusReceived).Sum(o => GetFinalAmount(o)),
            PendingRevenue = orders.Where(o => o.Status != "Отменен" && o.Status != "Отправлен" && o.Status != StatusReceived).Sum(o => GetFinalAmount(o))
        };
    }

    public async Task<bool> DeleteOrderAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
            return false;

        var childOrders = await _context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.ParentOrderId == orderId)
            .ToListAsync();

        // Сначала дочерние заказы (FK ParentOrderId), затем родитель.
        var ordersToDelete = childOrders.Append(order).ToList();

        foreach (var o in ordersToDelete)
        {
            foreach (var item in o.OrderItems)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product != null)
                    product.QuantityInStock += item.Quantity;
            }
        }

        foreach (var child in childOrders)
            _context.Orders.Remove(child);
        _context.Orders.Remove(order);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteOrderItemAsync(int orderId, int itemId)
    {
        var item = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .FirstOrDefaultAsync(oi => oi.OrderId == orderId && oi.Id == itemId);
        if (item == null || item.Order == null || item.Product == null)
            return false;

        if (item.Order.Status != "В сборке")
            return false;

        var order = item.Order;
        var product = item.Product;
        product.QuantityInStock += item.Quantity;
        order.TotalAmount -= item.ProductPrice * item.Quantity;
        order.UpdatedAt = DateTime.UtcNow;
        _context.OrderItems.Remove(item);

        await _context.SaveChangesAsync();

        await _queueService.PromoteNextAfterCartReleaseAsync(product.Id);

        var itemsLeft = await _context.OrderItems.CountAsync(oi => oi.OrderId == orderId);
        if (itemsLeft == 0)
        {
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> SetOrderItemAddedToParcelAsync(int orderId, int itemId, bool addedToParcel)
    {
        var item = await _context.OrderItems
            .Include(oi => oi.Order)
            .FirstOrDefaultAsync(oi => oi.OrderId == orderId && oi.Id == itemId);
        if (item == null || item.Order == null)
            return false;
        if (item.Order.Status != "В сборке")
            return false;
        item.AddedToParcel = addedToParcel;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<OrderDto> MarkOrderReceivedByCustomerAsync(int orderId, int userId, int? rating, string? comment)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Include(o => o.StatusHistories)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
            throw new InvalidOperationException("Заказ не найден");
        if (order.UserId != userId)
            throw new InvalidOperationException("Нет доступа к этому заказу");

        var hasChildren = await _context.Orders.AnyAsync(o => o.ParentOrderId == orderId);
        if (hasChildren)
            throw new InvalidOperationException("Подтвердите получение по каждой отправке отдельно — кнопка «Получен» у каждой части.");

        if (order.Status == StatusPartiallyShipped)
            throw new InvalidOperationException("Заказ ещё не полностью отправлен — дождитесь всех частей.");

        if (order.Status != "Отправлен")
            throw new InvalidOperationException("Подтвердить получение можно только для отправленного заказа");

        if (rating.HasValue && (rating.Value < 1 || rating.Value > 5))
            throw new InvalidOperationException("Оценка должна быть от 1 до 5");

        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (trimmed != null && trimmed.Length > 4000)
            trimmed = trimmed[..4000];

        var wantReview = rating.HasValue || !string.IsNullOrEmpty(trimmed);
        if (wantReview && await _context.OrderCustomerReviews.AnyAsync(r => r.OrderId == orderId))
            throw new InvalidOperationException("Отзыв по этому заказу уже оставлен");

        order.Status = StatusReceived;
        order.UpdatedAt = DateTime.UtcNow;
        order.StatusHistories.Add(new OrderStatusHistory
        {
            Status = StatusReceived,
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = userId
        });

        if (wantReview)
        {
            _context.OrderCustomerReviews.Add(new OrderCustomerReview
            {
                OrderId = orderId,
                UserId = userId,
                Rating = rating,
                Comment = trimmed,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        if (order.ParentOrderId.HasValue)
            await TryPromoteParentWhenAllChildrenReceivedAsync(order.ParentOrderId.Value, userId);
        else
        {
            try
            {
                await _referralService.ProcessOrderReceivedAsync(userId, orderId, GetFinalAmount(order));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Referral] ProcessOrderReceived failed for order {orderId}: {ex.Message}");
            }
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return MapToDto(order, user, wantReview);
    }

    public async Task<List<OrderCustomerReviewAdminDto>> GetCustomerReviewsAsync()
    {
        var reviews = await _context.OrderCustomerReviews
            .Include(r => r.Order)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return reviews.Select(MapOrderCustomerReviewToAdminDto).ToList();
    }

    public async Task<OrderCustomerReviewAdminDto> CreateAdminManualReviewAsync(CreateAdminManualReviewDto dto, int adminUserId)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
            throw new InvalidOperationException("Оценка должна быть от 1 до 5");

        var trimmedComment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();
        if (trimmedComment != null && trimmedComment.Length > 4000)
            trimmedComment = trimmedComment[..4000];

        var adminExists = await _context.Users.AnyAsync(u => u.Id == adminUserId);
        if (!adminExists)
            throw new InvalidOperationException("Пользователь администратора не найден");

        Order? order = null;
        var orderNo = dto.OrderNumber?.Trim();
        if (!string.IsNullOrEmpty(orderNo))
        {
            var found = await _context.Orders
                .Include(o => o.CustomerReview)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNo);
            if (found == null)
                throw new InvalidOperationException("Заказ с таким номером не найден.");
            if (found.CustomerReview != null)
                throw new InvalidOperationException("Отзыв по этому заказу уже существует.");
            order = found;
        }

        DateTime createdAt;
        if (!string.IsNullOrWhiteSpace(dto.CreatedDate) &&
            DateOnly.TryParse(dto.CreatedDate.Trim(), CultureInfo.InvariantCulture, out var dOnly))
            createdAt = dOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        else
            createdAt = dto.CreatedAtUtc ?? DateTime.UtcNow;

        var imagePaths = await SaveReviewImagesFromBase64Async(dto.ImagesBase64);
        var manualName = string.IsNullOrWhiteSpace(dto.CustomerName) ? null : dto.CustomerName.Trim();
        var manualPhone = string.IsNullOrWhiteSpace(dto.CustomerPhone) ? null : dto.CustomerPhone.Trim();

        var review = new OrderCustomerReview
        {
            OrderId = order?.Id,
            UserId = adminUserId,
            Rating = dto.Rating,
            Comment = trimmedComment,
            CreatedAtUtc = createdAt,
            ReviewImagesJson = imagePaths.Count > 0 ? JsonSerializer.Serialize(imagePaths) : null,
            ManualCustomerName = order == null ? manualName : null,
            ManualCustomerPhone = order == null ? manualPhone : null
        };
        _context.OrderCustomerReviews.Add(review);
        await _context.SaveChangesAsync();

        var saved = await _context.OrderCustomerReviews
            .Include(r => r.Order)
            .Include(r => r.User)
            .FirstAsync(r => r.Id == review.Id);

        return MapOrderCustomerReviewToAdminDto(saved);
    }

    public async Task<OrderCustomerReviewAdminDto> UpdateAdminManualReviewAsync(int reviewId, UpdateAdminManualReviewDto dto)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
            throw new InvalidOperationException("Оценка должна быть от 1 до 5");

        var review = await _context.OrderCustomerReviews
            .Include(r => r.Order)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reviewId)
            ?? throw new InvalidOperationException("Отзыв не найден");

        var trimmedComment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim();
        if (trimmedComment != null && trimmedComment.Length > 4000)
            trimmedComment = trimmedComment[..4000];

        var existingImages = DeserializeReviewImagePaths(review.ReviewImagesJson);
        var keepUrls = dto.KeepImageUrls ?? existingImages;
        var keepNormalized = new HashSet<string>(
            keepUrls.Select(NormalizeReviewImagePath),
            StringComparer.OrdinalIgnoreCase);
        var keptImages = existingImages
            .Where(path => keepNormalized.Contains(NormalizeReviewImagePath(path)))
            .ToList();

        foreach (var path in existingImages)
        {
            if (!keptImages.Contains(path))
                DeleteReviewImageFile(path);
        }

        var newPaths = await SaveReviewImagesFromBase64Async(dto.ImagesBase64);
        var mergedImages = keptImages.Concat(newPaths).ToList();

        if (string.IsNullOrWhiteSpace(trimmedComment) && mergedImages.Count == 0)
            throw new InvalidOperationException("Добавьте текст отзыва или хотя бы одно фото");

        review.Rating = dto.Rating;
        review.Comment = trimmedComment;
        review.ReviewImagesJson = mergedImages.Count > 0 ? JsonSerializer.Serialize(mergedImages) : null;

        if (!string.IsNullOrWhiteSpace(dto.CreatedDate) &&
            DateOnly.TryParse(dto.CreatedDate.Trim(), CultureInfo.InvariantCulture, out var dOnly))
            review.CreatedAtUtc = dOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        else if (dto.CreatedAtUtc.HasValue)
            review.CreatedAtUtc = dto.CreatedAtUtc.Value;

        if (!review.OrderId.HasValue)
        {
            var orderNo = dto.OrderNumber?.Trim();
            if (!string.IsNullOrEmpty(orderNo))
            {
                var found = await _context.Orders
                    .Include(o => o.CustomerReview)
                    .FirstOrDefaultAsync(o => o.OrderNumber == orderNo);
                if (found == null)
                    throw new InvalidOperationException("Заказ с таким номером не найден.");
                if (found.CustomerReview != null && found.CustomerReview.Id != reviewId)
                    throw new InvalidOperationException("Отзыв по этому заказу уже существует.");
                review.OrderId = found.Id;
                review.ManualCustomerName = null;
                review.ManualCustomerPhone = null;
            }
            else
            {
                review.ManualCustomerName = string.IsNullOrWhiteSpace(dto.CustomerName)
                    ? null
                    : dto.CustomerName.Trim();
                review.ManualCustomerPhone = string.IsNullOrWhiteSpace(dto.CustomerPhone)
                    ? null
                    : dto.CustomerPhone.Trim();
            }
        }

        await _context.SaveChangesAsync();

        var saved = await _context.OrderCustomerReviews
            .Include(r => r.Order)
            .Include(r => r.User)
            .FirstAsync(r => r.Id == reviewId);

        return MapOrderCustomerReviewToAdminDto(saved);
    }

    public async Task<bool> DeleteCustomerReviewAsync(int reviewId)
    {
        var r = await _context.OrderCustomerReviews.FirstOrDefaultAsync(x => x.Id == reviewId);
        if (r == null) return false;

        foreach (var path in DeserializeReviewImagePaths(r.ReviewImagesJson))
            DeleteReviewImageFile(path);

        _context.OrderCustomerReviews.Remove(r);
        await _context.SaveChangesAsync();
        return true;
    }

    private void DeleteReviewImageFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return;
        var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var full = Path.Combine(AppPaths.WwwRoot(_environment), relative);
        try
        {
            if (File.Exists(full))
                File.Delete(full);
        }
        catch
        {
            // ignore file errors
        }
    }

    private static string NormalizeReviewImagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var trimmed = path.Trim();
        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
    }

    private const string AbsentDisplay = "Отсутствует";

    private static OrderCustomerReviewAdminDto MapOrderCustomerReviewToAdminDto(OrderCustomerReview r)
    {
        string orderNumber;
        string customerName;
        string? customerPhone;

        if (r.OrderId.HasValue && r.Order != null)
        {
            orderNumber = string.IsNullOrWhiteSpace(r.Order.OrderNumber) ? AbsentDisplay : r.Order.OrderNumber;
            customerName = r.User?.FullName ?? r.User?.Username ?? r.Order.CustomerName;
            if (string.IsNullOrWhiteSpace(customerName))
                customerName = AbsentDisplay;
            customerPhone = r.Order.CustomerPhone ?? r.User?.Phone;
        }
        else
        {
            orderNumber = AbsentDisplay;
            customerName = string.IsNullOrWhiteSpace(r.ManualCustomerName) ? AbsentDisplay : r.ManualCustomerName!;
            customerPhone = string.IsNullOrWhiteSpace(r.ManualCustomerPhone) ? null : r.ManualCustomerPhone;
        }

        return new OrderCustomerReviewAdminDto
        {
            Id = r.Id,
            OrderId = r.OrderId,
            OrderNumber = orderNumber,
            UserId = r.UserId,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAtUtc = r.CreatedAtUtc,
            ImageUrls = DeserializeReviewImagePaths(r.ReviewImagesJson)
        };
    }

    private static List<string> DeserializeReviewImagePaths(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task<List<string>> SaveReviewImagesFromBase64Async(List<string>? images)
    {
        var paths = new List<string>();
        if (images == null || images.Count == 0) return paths;

        var uploadsFolder = Path.Combine(AppPaths.WwwRoot(_environment), "uploads");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        foreach (var base64Image in images)
        {
            if (string.IsNullOrWhiteSpace(base64Image)) continue;
            try
            {
                var base64Data = base64Image.Contains(',')
                    ? base64Image.Split(',')[1]
                    : base64Image;
                var imageBytes = Convert.FromBase64String(base64Data);
                var extension = ".jpg";
                if (imageBytes.Length > 2)
                {
                    if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50) extension = ".png";
                    else if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49) extension = ".gif";
                    else if (imageBytes[0] == 0x52 && imageBytes[1] == 0x49) extension = ".webp";
                }

                var fileName = $"{Guid.NewGuid()}{extension}";
                await File.WriteAllBytesAsync(Path.Combine(uploadsFolder, fileName), imageBytes);
                paths.Add($"/uploads/{fileName}");
            }
            catch
            {
                // пропускаем битое изображение
            }
        }

        return paths;
    }

    public async Task ApplyDiscountToOrdersAsync(IEnumerable<int> orderIds, string discountType, int? fixedPercent, int? condition1, int? condition3, int? condition5Plus)
    {
        var ids = orderIds.ToList();
        var orders = await _context.Orders.Where(o => ids.Contains(o.Id)).ToListAsync();
        foreach (var order in orders)
        {
            order.DiscountType = discountType;
            order.FixedDiscountPercent = fixedPercent;
            order.Condition1ItemPercent = condition1;
            order.Condition3ItemsPercent = condition3;
            order.Condition5PlusPercent = condition5Plus;
            order.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RemoveOrderDiscountAsync(int orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return false;
        order.DiscountType = "None";
        order.FixedDiscountPercent = null;
        order.Condition1ItemPercent = null;
        order.Condition3ItemsPercent = null;
        order.Condition5PlusPercent = null;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApplyOrderDiscountAsync(int orderId, int percent)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return false;
        order.DiscountType = "Fixed";
        order.FixedDiscountPercent = percent;
        order.Condition1ItemPercent = null;
        order.Condition3ItemsPercent = null;
        order.Condition5PlusPercent = null;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Удаляет все записи очереди резерва («беру» / сайт) по указанным товарам — после оформления заказа или продажи слоты недействительны.
    /// </summary>
    private async Task RemoveReserveQueueForProductsAsync(IEnumerable<int> productIds)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
            return;
        var entries = await _context.ReserveQueue
            .Where(r => ids.Contains(r.ProductId))
            .ToListAsync();
        if (entries.Count > 0)
            _context.ReserveQueue.RemoveRange(entries);
    }

    private static int GetEffectiveDiscountPercent(Order order)
    {
        if (string.IsNullOrEmpty(order.DiscountType) || order.DiscountType == "None")
            return 0;
        if (order.DiscountType == "Fixed" && order.FixedDiscountPercent.HasValue)
            return order.FixedDiscountPercent.Value;
        if (order.DiscountType == "ByCondition")
        {
            var itemCount = order.OrderItems.Sum(oi => oi.Quantity);
            if (itemCount >= 5 && order.Condition5PlusPercent.HasValue) return order.Condition5PlusPercent.Value;
            if (itemCount >= 3 && order.Condition3ItemsPercent.HasValue) return order.Condition3ItemsPercent.Value;
            if (order.Condition1ItemPercent.HasValue) return order.Condition1ItemPercent.Value;
        }
        return 0;
    }

    private static decimal GetFinalAmount(Order order)
    {
        var pct = GetEffectiveDiscountPercent(order);
        return order.TotalAmount * (100 - pct) / 100m;
    }

    private int? GetActorUserIdFromHttp()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return null;
        var v = user.FindFirst("UserId")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;
        return int.TryParse(v, out var id) ? id : null;
    }

    private static string ActorKindLabel(int? changedByUserId, int? orderUserId)
    {
        if (changedByUserId == null)
            return "Система";
        if (orderUserId.HasValue && changedByUserId.Value == orderUserId.Value)
            return "Клиент";
        return "Администратор";
    }

    private static OrderDto MapToDto(Order order, User? user = null, bool hasCustomerReview = false)
    {
        var history = (order.StatusHistories ?? new List<OrderStatusHistory>())
            .OrderBy(h => h.ChangedAtUtc)
            .Select(h => new OrderStatusHistoryDto
            {
                Status = NormalizeOrderStatusForApi(h.Status),
                ChangedAtUtc = h.ChangedAtUtc,
                ChangedByUserId = h.ChangedByUserId,
                ActorKind = ActorKindLabel(h.ChangedByUserId, order.UserId)
            })
            .ToList();

        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            CustomerEmail = order.CustomerEmail,
            CustomerAddress = order.CustomerAddress,
            DeliveryMethod = order.DeliveryMethod,
            Comment = order.Comment,
            TotalAmount = order.TotalAmount,
            FinalAmount = GetFinalAmount(order),
            Status = NormalizeOrderStatusForApi(order.Status),
            DiscountType = order.DiscountType ?? "None",
            FixedDiscountPercent = order.FixedDiscountPercent,
            Condition1ItemPercent = order.Condition1ItemPercent,
            Condition3ItemsPercent = order.Condition3ItemsPercent,
            Condition5PlusPercent = order.Condition5PlusPercent,
            OrderItems = order.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                ProductPrice = oi.ProductPrice,
                Quantity = oi.Quantity,
                Size = oi.Product?.Size,
                Color = oi.Product?.Color,
                Brand = oi.Product?.Brand,
                ImageUrl = oi.Product?.Images?.FirstOrDefault(),
                AddedToParcel = oi.AddedToParcel
            }).ToList(),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            CancelledAt = order.CancelledAt,
            CancellationReason = order.CancellationReason,
            UserId = order.UserId,
            CustomerProfileLink = order.CustomerProfileLink,
            StatusHistory = history,
            HasCustomerReview = hasCustomerReview,
            ParentOrderId = order.ParentOrderId
        };
    }

    private static decimal SumItemsAmount(IEnumerable<OrderItem> items) =>
        items.Sum(i => i.ProductPrice * i.Quantity);

    private Order CreateSplitChildOrder(Order parent, decimal totalAmount, string orderNumberSuffix, string status)
    {
        return new Order
        {
            ParentOrderId = parent.Id,
            OrderNumber = $"{parent.OrderNumber}-{orderNumberSuffix}",
            UserId = parent.UserId,
            CustomerName = parent.CustomerName,
            CustomerProfileLink = parent.CustomerProfileLink,
            CustomerPhone = parent.CustomerPhone,
            CustomerEmail = parent.CustomerEmail,
            CustomerAddress = parent.CustomerAddress,
            DeliveryMethod = parent.DeliveryMethod,
            Comment = parent.Comment,
            TotalAmount = totalAmount,
            Status = status,
            DiscountType = parent.DiscountType,
            FixedDiscountPercent = parent.FixedDiscountPercent,
            Condition1ItemPercent = parent.Condition1ItemPercent,
            Condition3ItemsPercent = parent.Condition3ItemsPercent,
            Condition5PlusPercent = parent.Condition5PlusPercent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private void AddStatusHistory(int orderId, string status, int? changedByUserId = null)
    {
        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId,
            Status = status,
            ChangedAtUtc = DateTime.UtcNow,
            ChangedByUserId = changedByUserId ?? GetActorUserIdFromHttp()
        });
    }

    private async Task<OrderStatusUpdateOutcome> SplitAndShipPartialOrderAsync(
        Order parent,
        List<OrderItem> inParcelItems,
        List<OrderItem> notInParcelItems)
    {
        var actorId = GetActorUserIdFromHttp();
        var shippedTotal = SumItemsAmount(inParcelItems);
        var pendingTotal = SumItemsAmount(notInParcelItems);

        var shippedChild = CreateSplitChildOrder(parent, shippedTotal, "1", "Отправлен");
        var pendingChild = CreateSplitChildOrder(parent, pendingTotal, "2", "В сборке");

        _context.Orders.Add(shippedChild);
        _context.Orders.Add(pendingChild);
        await _context.SaveChangesAsync();

        foreach (var item in inParcelItems)
        {
            item.OrderId = shippedChild.Id;
            item.AddedToParcel = true;
        }
        foreach (var item in notInParcelItems)
        {
            item.OrderId = pendingChild.Id;
            item.AddedToParcel = false;
        }

        parent.Status = StatusPartiallyShipped;
        parent.UpdatedAt = DateTime.UtcNow;

        AddStatusHistory(parent.Id, StatusPartiallyShipped, actorId);
        AddStatusHistory(shippedChild.Id, "Отправлен", actorId);
        AddStatusHistory(pendingChild.Id, "В сборке", actorId);

        var shippedProductIds = inParcelItems.Select(i => i.ProductId).Distinct().ToList();
        await RemoveReserveQueueForProductsAsync(shippedProductIds);

        await _context.SaveChangesAsync();

        return new OrderStatusUpdateOutcome(true);
    }

    private async Task TryPromoteParentWhenAllChildrenShippedAsync(int parentOrderId)
    {
        var parent = await _context.Orders.FirstOrDefaultAsync(o => o.Id == parentOrderId);
        if (parent == null || parent.Status != StatusPartiallyShipped)
            return;

        var children = await _context.Orders.Where(o => o.ParentOrderId == parentOrderId).ToListAsync();
        if (children.Count == 0)
            return;

        var allShipped = children.All(c =>
            c.Status == "Отправлен" || c.Status == StatusReceived);
        if (!allShipped)
            return;

        parent.Status = "Отправлен";
        parent.UpdatedAt = DateTime.UtcNow;
        AddStatusHistory(parent.Id, "Отправлен", null);
        await _context.SaveChangesAsync();
    }

    private async Task TryPromoteParentWhenAllChildrenReceivedAsync(int parentOrderId, int userId)
    {
        var parent = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == parentOrderId);
        if (parent == null)
            return;

        var children = await _context.Orders.Where(o => o.ParentOrderId == parentOrderId).ToListAsync();
        if (children.Count == 0)
            return;

        if (!children.All(c => c.Status == StatusReceived))
            return;

        parent.Status = StatusReceived;
        parent.UpdatedAt = DateTime.UtcNow;
        AddStatusHistory(parent.Id, StatusReceived, userId);
        await _context.SaveChangesAsync();

        try
        {
            await _referralService.ProcessOrderReceivedAsync(userId, parentOrderId, GetFinalAmount(parent));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Referral] ProcessOrderReceived failed for parent order {parentOrderId}: {ex.Message}");
        }
    }
}

