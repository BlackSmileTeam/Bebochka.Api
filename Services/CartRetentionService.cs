using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Data;
using Bebochka.Api.Models;

namespace Bebochka.Api.Services;

/// <summary>
/// Авто-очистка корзин/очереди и отмена неоплаченных заказов старше 24 часов.
/// </summary>
public class CartRetentionService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HoldPeriod = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CartRetentionService> _logger;

    public CartRetentionService(IServiceScopeFactory scopeFactory, ILogger<CartRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRetentionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CartRetentionService failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessRetentionAsync(CancellationToken ct)
    {
        if (!BackgroundJobSettings.ExecuteWork)
        {
            _logger.LogDebug("CartRetentionService tick (work disabled, timer only)");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queueService = scope.ServiceProvider.GetRequiredService<WebReserveQueueService>();

        var cutoff = DateTime.UtcNow - HoldPeriod;
        var productIdsToPromote = new HashSet<int>();

        // 1) Корзина старше 24ч -> снимаем бронь.
        var staleCartItems = await db.CartItems
            .Where(c => c.UpdatedAt < cutoff)
            .ToListAsync(ct);
        foreach (var item in staleCartItems)
            productIdsToPromote.Add(item.ProductId);
        if (staleCartItems.Count > 0)
            db.CartItems.RemoveRange(staleCartItems);

        // 2) Очередь старше 24ч -> удаляем устаревшие записи.
        var staleQueueItems = await db.ReserveQueue
            .Where(q => q.CreatedAt < cutoff)
            .ToListAsync(ct);
        if (staleQueueItems.Count > 0)
            db.ReserveQueue.RemoveRange(staleQueueItems);

        // 3) Неоплаченные заказы старше 24ч -> авто-отмена + возврат стока.
        var staleUnpaidOrders = await db.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.Status == "Ожидает оплату" && o.CreatedAt < cutoff)
            .ToListAsync(ct);

        foreach (var order in staleUnpaidOrders)
        {
            foreach (var item in order.OrderItems)
            {
                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId, ct);
                if (product != null)
                {
                    product.QuantityInStock += item.Quantity;
                    productIdsToPromote.Add(item.ProductId);
                }
            }

            order.Status = "Отменен";
            order.CancellationReason = "Автоматически отменен: не оплачено в течение 24 часов";
            order.CancelledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = "Отменен",
                ChangedAtUtc = DateTime.UtcNow,
                ChangedByUserId = null
            });
        }

        if (staleCartItems.Count == 0 && staleQueueItems.Count == 0 && staleUnpaidOrders.Count == 0)
            return;

        await db.SaveChangesAsync(ct);

        foreach (var productId in productIdsToPromote)
            await queueService.PromoteNextAfterCartReleaseAsync(productId, ct);

        _logger.LogInformation(
            "Retention cleanup completed: cart={CartCount}, queue={QueueCount}, unpaidOrders={OrderCount}",
            staleCartItems.Count, staleQueueItems.Count, staleUnpaidOrders.Count);
    }
}
