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
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;

    public OrderService(AppDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
    {
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

        // Удаляем товары из корзины пользователя
        var cartItems = await _context.CartItems
            .Where(c => c.SessionId == dto.SessionId)
            .ToListAsync();
        _context.CartItems.RemoveRange(cartItems);

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

        return MapToDto(order);
    }

    public async Task<List<OrderDto>> GetAllOrdersAsync()
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        // Загружаем информацию о пользователях для заказов
        var userIds = orders.Where(o => o.UserId.HasValue).Select(o => o.UserId!.Value).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u);

        return orders.Select(o => MapToDto(o, users.GetValueOrDefault(o.UserId ?? 0))).ToList();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return null;

        User? user = null;
        if (order.UserId.HasValue)
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Id == order.UserId.Value);
        }

        return MapToDto(order, user);
    }

    public async Task<List<OrderDto>> GetUserOrdersAsync(int userId)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        return orders.Select(o => MapToDto(o, user)).ToList();
    }

    public async Task<bool> CancelOrderAsync(int orderId, string? reason = null)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return false;

        if (order.Status == "Доставлен" || order.Status == "Отменен")
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

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
    {
        var validStatuses = new[] { "В сборке", "Ожидает оплату", "В пути", "Доставлен", "Отменен" };
        if (!validStatuses.Contains(status))
            return false;

        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
            return false;

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        if (status == "Отменен" && order.CancelledAt == null)
        {
            order.CancelledAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<OrderStatisticsDto> GetStatisticsAsync()
    {
        var orders = await _context.Orders.ToListAsync();

        return new OrderStatisticsDto
        {
            TotalOrders = orders.Count,
            PendingOrders = orders.Count(o => o.Status == "В сборке"),
            AwaitingPaymentOrders = orders.Count(o => o.Status == "Ожидает оплату"),
            InTransitOrders = orders.Count(o => o.Status == "В пути"),
            DeliveredOrders = orders.Count(o => o.Status == "Доставлен"),
            CancelledOrders = orders.Count(o => o.Status == "Отменен"),
            TotalRevenue = orders.Where(o => o.Status == "Доставлен").Sum(o => o.TotalAmount),
            PendingRevenue = orders.Where(o => o.Status != "Отменен" && o.Status != "Доставлен").Sum(o => o.TotalAmount)
        };
    }

    private static OrderDto MapToDto(Order order, User? user = null)
    {
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
            Status = order.Status,
            OrderItems = order.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                ProductPrice = oi.ProductPrice,
                Quantity = oi.Quantity
            }).ToList(),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            CancelledAt = order.CancelledAt,
            CancellationReason = order.CancellationReason,
            UserId = order.UserId,
            TelegramUserId = user?.TelegramUserId,
            TelegramUsername = user?.FullName // Используем FullName как имя пользователя для отображения
        };
    }
}

