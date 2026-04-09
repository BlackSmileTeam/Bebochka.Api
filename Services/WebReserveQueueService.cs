using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Bebochka.Api.Data;
using Bebochka.Api.Models;

namespace Bebochka.Api.Services;

/// <summary>
/// Очередь резерва для сайта: при освобождении товара из корзины — в корзину следующему пользователю.
/// Продвижение выполняется в отдельном scope с новым DbContext, чтобы гарантированно видеть уже зафиксированное удаление строки корзины.
/// </summary>
public class WebReserveQueueService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebReserveQueueService> _logger;

    public WebReserveQueueService(IServiceScopeFactory scopeFactory, ILogger<WebReserveQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private static string SessionKeyForUser(int userId) => $"uid:{userId}";

    /// <summary>
    /// После удаления позиции из корзины — передаёт товар следующему в web-очереди (FIFO по CreatedAt).
    /// </summary>
    public async Task PromoteNextAfterCartReleaseAsync(int productId, CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var next = await context.ReserveQueue
                .Where(rq => rq.ProductId == productId && rq.WebUserId != null)
                .OrderBy(rq => rq.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (next?.WebUserId == null)
            {
                _logger.LogDebug("No web queue entry for product {ProductId}", productId);
                return;
            }

            var userId = next.WebUserId.Value;
            var product = await context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId, ct);

            if (product == null)
            {
                context.ReserveQueue.Remove(next);
                await context.SaveChangesAsync(ct);
                return;
            }

            // Не используем CartAvailableAt: освобождение брони из корзины — явная передача слота следующему в очереди.

            var reservedExceptThisUser = await context.CartItems
                .Where(c => c.ProductId == productId)
                .Where(c => c.UserId == null || c.UserId != userId)
                .SumAsync(c => (int?)c.Quantity, ct) ?? 0;

            var available = product.QuantityInStock - reservedExceptThisUser;
            if (available <= 0)
            {
                _logger.LogInformation(
                    "Promote queue skipped for product {ProductId}: no free quantity (others reserved: {R}, stock: {S})",
                    productId, reservedExceptThisUser, product.QuantityInStock);
                return;
            }

            var sessionId = SessionKeyForUser(userId);
            var existing = await context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId, ct);

            if (existing != null)
            {
                existing.Quantity = Math.Min(existing.Quantity + 1, available);
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                context.CartItems.Add(new CartItem
                {
                    SessionId = sessionId,
                    UserId = userId,
                    ProductId = productId,
                    Quantity = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            context.ReserveQueue.Remove(next);
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Promoted product {ProductId} from queue to user {UserId}", productId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PromoteNextAfterCartReleaseAsync failed for product {ProductId}", productId);
        }
    }
}
