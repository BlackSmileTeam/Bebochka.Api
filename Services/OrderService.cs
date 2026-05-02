using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
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

    private static readonly string[] AdminSelectableStatuses =
    {
        "Формирование заказа", "Ожидает оплату", "В сборке", "На доставку", "Отправлен", "Отменен"
    };

    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ITelegramNotificationService _telegramService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrderService(AppDbContext context, IEmailService emailService, ITelegramNotificationService telegramService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _emailService = emailService;
        _telegramService = telegramService;
        _httpContextAccessor = httpContextAccessor;
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

        var reviewedOrderIds = (await _context.OrderCustomerReviews.Select(r => r.OrderId).ToListAsync()).ToHashSet();

        return orders.Select(o => MapToDto(o, users.GetValueOrDefault(o.UserId ?? 0), reviewedOrderIds.Contains(o.Id))).ToList();
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

        var reviewedOrderIds = (await _context.OrderCustomerReviews
            .Where(r => r.UserId == userId)
            .Select(r => r.OrderId)
            .ToListAsync()).ToHashSet();

        return orders.Select(o => MapToDto(o, user, reviewedOrderIds.Contains(o.Id))).ToList();
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

    public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
    {
        if (status == StatusReceived)
            return false;

        if (!AdminSelectableStatuses.Contains(status))
            return false;

        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
            return false;

        // После подтверждения получения клиентом статус менять нельзя.
        if (order.Status == StatusReceived)
            return false;

        var previousStatus = order.Status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        if (status == "Отменен" && order.CancelledAt == null)
        {
            order.CancelledAt = DateTime.UtcNow;
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

        var user = order.UserId.HasValue ? await _context.Users.FindAsync(order.UserId.Value) : null;
        var telegramUserId = user?.TelegramUserId;
        if (telegramUserId.HasValue)
        {
            var statusText = status switch
            {
                "Формирование заказа" => "формирование заказа",
                "Ожидает оплату" => "ожидает оплату",
                "В сборке" => "в сборке",
                "На доставку" => "на доставку",
                "Отправлен" => "отправлен",
                "Отменен" => "отменён",
                _ => status
            };
            if (status == "Ожидает оплату" && previousStatus != "Ожидает оплату")
            {
                await _telegramService.SendMessageAsync(telegramUserId.Value,
                    $"<b>Заказ {order.OrderNumber} успешно оформлен</b>\nНеобходимо оплатить заказ. После оплаты мы соберём и отправим его.");
            }
            else
            {
                await _telegramService.SendMessageAsync(telegramUserId.Value,
                    $"<b>Статус заказа {order.OrderNumber} изменён</b>\nНовый статус: {statusText}.");
            }

            if (status == "В сборке")
            {
                var orderWithItems = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                if (orderWithItems?.OrderItems != null)
                {
                    var req = _httpContextAccessor.HttpContext?.Request;
                    var baseUrl = req != null ? $"{req.Scheme}://{req.Host}" : null;
                    if (!string.IsNullOrEmpty(baseUrl))
                    {
                        foreach (var oi in orderWithItems.OrderItems.Where(oi => oi.Product != null))
                        {
                            var p = oi.Product!;
                            var imagePaths = p.Images ?? new List<string>();
                            var imageUrls = imagePaths
                                .Select(path => path.StartsWith("http") ? path : (path.StartsWith("/") ? $"{baseUrl.TrimEnd('/')}{path}" : $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}"))
                                .ToList();
                            if (imageUrls.Count > 0)
                            {
                                var caption = $"<b>{p.Name}</b>";
                                if (!string.IsNullOrEmpty(p.Brand)) caption += $"\nБренд: {p.Brand}";
                                if (!string.IsNullOrEmpty(p.Size)) caption += $"\nРазмер: {p.Size}";
                                if (!string.IsNullOrEmpty(p.Color)) caption += $"\nЦвет: {p.Color}";
                                caption += $"\nЦена: {p.Price:N0} ₽";
                                await _telegramService.SendPhotosToUserByUrlsAsync(telegramUserId.Value, imageUrls, caption);
                            }
                        }
                    }
                }
            }
        }

        return true;
    }

    public async Task<OrderStatisticsDto> GetStatisticsAsync()
    {
        var orders = await _context.Orders.ToListAsync();

        return new OrderStatisticsDto
        {
            TotalOrders = orders.Count,
            FormingOrders = orders.Count(o => o.Status == "Формирование заказа"),
            AwaitingPaymentOrders = orders.Count(o => o.Status == "Ожидает оплату"),
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

        if (item.TelegramCommentChatId.HasValue && item.TelegramCommentMessageId.HasValue)
        {
            try
            {
                await _telegramService.DeleteMessageAsync(item.TelegramCommentChatId.Value, item.TelegramCommentMessageId.Value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteOrderItem: failed to delete Telegram comment: {ex.Message}");
            }
        }

        var order = item.Order;
        var product = item.Product;
        // Остаток не трогаем для заказов через канал (при брони из канала мы его не уменьшали)
        if (!item.TelegramCommentChatId.HasValue)
            product.QuantityInStock += item.Quantity;
        order.TotalAmount -= item.ProductPrice * item.Quantity;
        order.UpdatedAt = DateTime.UtcNow;
        _context.OrderItems.Remove(item);

        var nextQueue = await _context.ReserveQueue
            .Where(rq => rq.ProductId == product.Id && rq.WebUserId == null && rq.TelegramUserId != null)
            .OrderBy(rq => rq.CreatedAt)
            .FirstOrDefaultAsync();

        if (nextQueue != null)
        {
            var nextUser = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == nextQueue.TelegramUserId);
            if (nextUser != null)
            {
                var customerName = $"{nextQueue.FirstName ?? ""} {nextQueue.LastName ?? ""}".Trim();
                if (string.IsNullOrEmpty(customerName))
                    customerName = !string.IsNullOrEmpty(nextQueue.Username) ? $"@{nextQueue.Username}" : nextUser.FullName ?? nextUser.Username ?? $"Telegram {nextQueue.TelegramUserId}";
                var phone = nextQueue.CustomerPhone ?? "";

                var newOrderItem = new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductPrice = product.Price,
                    Quantity = 1,
                    TelegramCommentChatId = nextQueue.CommentChatId != 0 ? nextQueue.CommentChatId : null,
                    TelegramCommentMessageId = nextQueue.CommentMessageId != 0 ? nextQueue.CommentMessageId : null
                };

                var statusesAllowedToAdd = new[] { "Формирование заказа", "Ожидает оплату" };
                var existingOrder = await _context.Orders
                    .Include(o => o.OrderItems)
                    .Where(o => o.UserId == nextUser.Id && statusesAllowedToAdd.Contains(o.Status))
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                var nextUserProfileLink = nextUser.TelegramUserId.HasValue ? "tg://openmessage?user_id=" + nextUser.TelegramUserId.Value : null;
                if (existingOrder != null)
                {
                    existingOrder.OrderItems.Add(newOrderItem);
                    existingOrder.TotalAmount += product.Price;
                    existingOrder.CustomerName = customerName;
                    existingOrder.CustomerProfileLink = nextUserProfileLink ?? existingOrder.CustomerProfileLink;
                    if (!string.IsNullOrWhiteSpace(phone)) existingOrder.CustomerPhone = phone;
                    existingOrder.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
                    var newOrder = new Order
                    {
                        OrderNumber = orderNumber,
                        UserId = nextUser.Id,
                        CustomerName = customerName,
                        CustomerProfileLink = nextUserProfileLink,
                        CustomerPhone = phone,
                        CustomerEmail = nextUser.Email,
                        TotalAmount = product.Price,
                        Status = "Ожидает оплату",
                        OrderItems = new List<OrderItem> { newOrderItem },
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    newOrder.StatusHistories.Add(new OrderStatusHistory
                    {
                        Status = newOrder.Status,
                        ChangedAtUtc = DateTime.UtcNow,
                        ChangedByUserId = nextUser.Id
                    });
                    _context.Orders.Add(newOrder);
                }
                // При передаче следующему в очереди остаток не уменьшаем (заказы через канал не меняют остаток)
                // Товар снова в одном заказе — остальные в очереди по этому товару неактуальны
                await RemoveReserveQueueForProductsAsync(new[] { product.Id });
            }
            else
            {
                _context.ReserveQueue.Remove(nextQueue);
            }
        }

        await _context.SaveChangesAsync();

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

    public async Task<ReserveFromTelegramResultDto> ReserveFromTelegramAsync(string channelId, int messageId, long telegramUserId, string? username, string? firstName, string? lastName, string? customerPhone = null, long? commentChatId = null, int? commentMessageId = null)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.TelegramChatId == channelId && p.TelegramMessageId == messageId);
        if (product == null)
            return new ReserveFromTelegramResultDto { Success = false, Reason = "ProductNotFound" };

        var activeStatuses = new[] { "Ожидает оплату", "В сборке", "На доставку", "Отправлен", StatusReceived };
        var alreadyReserved = await _context.OrderItems
            .AnyAsync(oi => oi.ProductId == product.Id && _context.Orders.Any(o => o.Id == oi.OrderId && activeStatuses.Contains(o.Status)));
        if (alreadyReserved)
        {
            var queueEntry = new ReserveQueue
            {
                ProductId = product.Id,
                ChannelId = channelId,
                PostMessageId = messageId,
                TelegramUserId = telegramUserId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                CustomerPhone = customerPhone,
                CommentChatId = commentChatId ?? 0,
                CommentMessageId = commentMessageId ?? 0,
                CreatedAt = DateTime.UtcNow
            };
            if (commentChatId.HasValue && commentMessageId.HasValue)
            {
                queueEntry.CommentChatId = commentChatId.Value;
                queueEntry.CommentMessageId = commentMessageId.Value;
            }
            _context.ReserveQueue.Add(queueEntry);
            await _context.SaveChangesAsync();
            return new ReserveFromTelegramResultDto { Success = false, Reason = "AlreadyReserved" };
        }

        // Бронировать может только один пользователь, товар один — остаток не проверяем и не уменьшаем.
        // Пользователь: если есть в базе — берём его; если нет — создаём (товар попадает в существующий заказ в начальном статусе или создаётся новый).
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId);
        if (user == null)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var newUsername = $"telegram_{telegramUserId}_{timestamp}";
            user = new User
            {
                Username = newUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                TelegramUserId = telegramUserId,
                FullName = $"{firstName ?? ""} {lastName ?? ""}".Trim(),
                IsActive = true,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow
            };
            if (string.IsNullOrWhiteSpace(user.FullName))
                user.FullName = !string.IsNullOrEmpty(username) ? $"@{username}" : null;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // Имя заказчика: из Telegram (имя/фамилия или @username), иначе из профиля User
        var customerName = $"{firstName ?? ""} {lastName ?? ""}".Trim();
        if (string.IsNullOrEmpty(customerName))
            customerName = !string.IsNullOrEmpty(username) ? $"@{username}" : null;
        if (string.IsNullOrEmpty(customerName))
            customerName = user.FullName ?? user.Username;
        if (string.IsNullOrEmpty(customerName))
            customerName = $"Telegram {telegramUserId}";

        var customerProfileLink = !string.IsNullOrEmpty(username)
            ? "https://t.me/" + username.TrimStart('@')
            : "tg://openmessage?user_id=" + telegramUserId;

        var orderItem = new OrderItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            ProductPrice = product.Price,
            Quantity = 1,
            TelegramCommentChatId = commentChatId,
            TelegramCommentMessageId = commentMessageId
        };

        // Добавляем в существующий заказ только если статус «Формирование заказа» или «Ожидает оплату»; иначе создаём новый
        var statusesAllowedToAdd = new[] { "Формирование заказа", "Ожидает оплату" };
        var existingOrder = await _context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.UserId == user.Id && statusesAllowedToAdd.Contains(o.Status))
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        Order order;
        var phone = !string.IsNullOrWhiteSpace(customerPhone) ? customerPhone.Trim() : "";

        if (existingOrder != null)
        {
            order = existingOrder;
            order.OrderItems.Add(orderItem);
            order.TotalAmount += product.Price;
            order.CustomerName = customerName;
            order.CustomerProfileLink = customerProfileLink;
            if (!string.IsNullOrEmpty(phone))
                order.CustomerPhone = phone;
            order.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            order = new Order
            {
                OrderNumber = orderNumber,
                UserId = user.Id,
                CustomerName = customerName,
                CustomerProfileLink = customerProfileLink,
                CustomerPhone = phone,
                CustomerEmail = user.Email,
                TotalAmount = product.Price,
                Status = "Ожидает оплату",
                OrderItems = new List<OrderItem> { orderItem },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            order.StatusHistories.Add(new OrderStatusHistory
            {
                Status = order.Status,
                ChangedAtUtc = DateTime.UtcNow,
                ChangedByUserId = user.Id
            });
            _context.Orders.Add(order);
        }

        // Количество на складе при оформлении заказа через канал не уменьшаем
        await RemoveReserveQueueForProductsAsync(new[] { product.Id });
        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendOrderNotificationAsync(order);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send order email for telegram reserve: {ex.Message}");
        }

        var hasReview = await _context.OrderCustomerReviews.AnyAsync(r => r.OrderId == order.Id);
        return new ReserveFromTelegramResultDto { Success = true, Order = MapToDto(order, user, hasReview) };
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

        return reviews.Select(r => new OrderCustomerReviewAdminDto
        {
            Id = r.Id,
            OrderId = r.OrderId,
            OrderNumber = r.Order?.OrderNumber ?? $"#{r.OrderId}",
            UserId = r.UserId,
            CustomerName = r.User?.FullName
                ?? r.User?.Username
                ?? r.Order?.CustomerName
                ?? $"Пользователь #{r.UserId}",
            CustomerPhone = r.Order?.CustomerPhone ?? r.User?.Phone,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAtUtc = r.CreatedAtUtc
        }).ToList();
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
                Status = h.Status,
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
            Status = order.Status,
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
            CustomerProfileLink = order.CustomerProfileLink ?? (user?.TelegramUserId != null ? "tg://openmessage?user_id=" + user.TelegramUserId : null),
            TelegramUserId = user?.TelegramUserId,
            TelegramUsername = user != null ? (user.FullName ?? (user.Username != null && !user.Username.StartsWith("telegram_") ? user.Username : null)) ?? order.CustomerName : null,
            StatusHistory = history,
            HasCustomerReview = hasCustomerReview
        };
    }
}

