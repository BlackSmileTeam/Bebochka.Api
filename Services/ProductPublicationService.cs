using Microsoft.Extensions.Logging;

namespace Bebochka.Api.Services;

/// <summary>
/// Background service that checks for products ready for publication and sends notifications
/// </summary>
public class ProductPublicationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductPublicationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute

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
            var utcNow = DateTime.UtcNow;
            _logger.LogDebug("Checking for publications at UTC time: {UtcNow}", utcNow);

            // Get products that were just published (in the last 10 minutes)
            var readyProducts = await productService.GetProductsReadyForPublicationAsync();

            _logger.LogDebug("Found {Count} products ready for publication (UTC: {UtcNow})", readyProducts.Count, utcNow);

            if (readyProducts.Any())
            {
                _logger.LogInformation("Found {Count} products ready for publication at {UtcNow} UTC", readyProducts.Count, utcNow);
                
                foreach (var product in readyProducts)
                {
                    _logger.LogInformation("Product {ProductId} '{ProductName}' published at {PublishedAt} UTC", 
                        product.Id, product.Name, product.PublishedAt);
                }

                // Send notification to all Telegram users
                var notificationMessage = "Уважаемые дамы, каталог был обновлен. Успевайте забронировать товар!";
                var sentCount = await telegramService.SendBroadcastMessageAsync(notificationMessage);

                _logger.LogInformation("Publication notification sent to {SentCount} users at {UtcNow} UTC", sentCount, utcNow);
            }
            else
            {
                _logger.LogDebug("No products ready for publication at {UtcNow} UTC", utcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for publications");
        }
    }
}

