using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq;
using Bebochka.Api.Helpers;

namespace Bebochka.Api.Services;

/// <summary>
/// Background service that checks for products ready for publication and sends notifications
/// </summary>
public class ProductPublicationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductPublicationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute
    // Track which products have already received notifications (productId -> notification sent time)
    private readonly ConcurrentDictionary<int, DateTime> _notifiedProducts = new();

    public ProductPublicationService(
        IServiceProvider serviceProvider,
        ILogger<ProductPublicationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProductPublicationService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndNotifyPublicationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProductPublicationService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("ProductPublicationService stopped");
    }

    private async Task CheckAndNotifyPublicationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramNotificationService>();

        try
        {
            var moscowNow = DateTimeHelper.GetMoscowTime();
            var utcNow = DateTime.UtcNow;
            
            // Clean up old entries from memory (older than 1 hour) to prevent memory leaks
            var oneHourAgo = utcNow.AddHours(-1);
            var keysToRemove = _notifiedProducts
                .Where(kvp => kvp.Value < oneHourAgo)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in keysToRemove)
            {
                _notifiedProducts.TryRemove(key, out _);
            }

            _logger.LogDebug("Checking for publications at Moscow time: {MoscowNow} (UTC: {UtcNow})", moscowNow, utcNow);

            // Get products that were just published (in the last 5 minutes)
            // Using a narrow window to minimize duplicates, but wide enough to catch products if service was briefly down
            var readyProducts = await productService.GetProductsReadyForPublicationAsync();

            // Filter out products that have already received notifications
            var newProducts = readyProducts
                .Where(p => !_notifiedProducts.ContainsKey(p.Id))
                .ToList();

            _logger.LogDebug("Found {TotalCount} products ready for publication, {NewCount} new ones (Moscow: {MoscowNow})", 
                readyProducts.Count, newProducts.Count, moscowNow);

            if (newProducts.Any())
            {
                _logger.LogInformation("Found {Count} new products ready for publication at {MoscowNow} Moscow time", newProducts.Count, moscowNow);
                
                foreach (var product in newProducts)
                {
                    _logger.LogInformation("Product {ProductId} '{ProductName}' published at {PublishedAt} Moscow time", 
                        product.Id, product.Name, product.PublishedAt);
                    
                    // Mark as notified in memory
                    _notifiedProducts.TryAdd(product.Id, utcNow);
                }

                // Send notification to all Telegram users (only once per batch of new products)
                var notificationMessage = "Уважаемые дамы, каталог был обновлен. Успевайте забронировать товар!";
                var sentCount = await telegramService.SendBroadcastMessageAsync(notificationMessage);

                _logger.LogInformation("Publication notification sent to {SentCount} users at {MoscowNow} Moscow time", sentCount, moscowNow);
            }
            else
            {
                _logger.LogDebug("No new products ready for publication at {MoscowNow} Moscow time", moscowNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for publications");
        }
    }
}

