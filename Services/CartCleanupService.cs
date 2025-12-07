using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Bebochka.Api.Data;

namespace Bebochka.Api.Services;

/// <summary>
/// Background service for cleaning up expired cart reservations
/// </summary>
public class CartCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CartCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5); // Проверяем каждые 5 минут
    private readonly TimeSpan _reservationTimeout = TimeSpan.FromMinutes(20); // Резерв истекает через 20 минут

    public CartCleanupService(IServiceProvider serviceProvider, ILogger<CartCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cart cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredReservationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired cart reservations");
            }

            // Ждем перед следующей проверкой
            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Cart cleanup service stopped");
    }

    private async Task CleanupExpiredReservationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var expirationTime = DateTime.UtcNow.Subtract(_reservationTimeout);
            var expiredItems = await context.CartItems
                .Where(c => c.UpdatedAt < expirationTime)
                .ToListAsync(cancellationToken);

            if (expiredItems.Any())
            {
                var count = expiredItems.Count;
                context.CartItems.RemoveRange(expiredItems);
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation($"Cleaned up {count} expired cart reservations (older than {_reservationTimeout.TotalMinutes} minutes)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired cart reservations");
        }
    }
}

