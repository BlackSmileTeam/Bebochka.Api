using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Bebochka.Api.Data;
using Bebochka.Api.Helpers;

namespace Bebochka.Api.Services;

/// <summary>
/// Background service that checks for scheduled announcements and sends them
/// </summary>
public class AnnouncementService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnnouncementService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute

    public AnnouncementService(
        IServiceProvider serviceProvider,
        ILogger<AnnouncementService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AnnouncementService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendAnnouncementsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AnnouncementService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("AnnouncementService stopped");
    }

    private async Task CheckAndSendAnnouncementsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramNotificationService>();

        try
        {
            // ScheduledAt is stored as Moscow time, so we compare with current Moscow time
            var moscowNow = DateTimeHelper.GetMoscowTime();
            // Check for announcements scheduled in the last 5 minutes
            var fiveMinutesAgo = moscowNow.AddMinutes(-5);

            var readyAnnouncements = await context.Announcements
                .Where(a => !a.IsSent && 
                           a.ScheduledAt <= moscowNow && 
                           a.ScheduledAt > fiveMinutesAgo)
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync(cancellationToken);

            if (!readyAnnouncements.Any())
            {
                _logger.LogDebug("No announcements ready to send at {MoscowNow} Moscow time", moscowNow);
                return;
            }

            _logger.LogInformation("Found {Count} announcements ready to send at {MoscowNow} Moscow time", 
                readyAnnouncements.Count, moscowNow);

            foreach (var announcement in readyAnnouncements)
            {
                try
                {
                    int sentCount = 0;
                    
                    if (announcement.CollageImages != null && announcement.CollageImages.Count > 0)
                    {
                        // Send message with photos
                        sentCount = await telegramService.SendBroadcastWithPhotosAsync(
                            announcement.Message, 
                            announcement.CollageImages);
                    }
                    else
                    {
                        // Send text message only
                        sentCount = await telegramService.SendBroadcastMessageAsync(announcement.Message);
                    }

                    // Mark as sent
                    announcement.IsSent = true;
                    announcement.SentAt = DateTime.UtcNow;
                    announcement.SentCount = sentCount;
                    
                    await context.SaveChangesAsync(cancellationToken);
                    
                    _logger.LogInformation("Announcement {AnnouncementId} sent to {SentCount} users", 
                        announcement.Id, sentCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending announcement {AnnouncementId}", announcement.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for announcements");
        }
    }
}

