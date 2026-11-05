using Microsoft.EntityFrameworkCore;
using menu_backend.Services.Interfaces;

namespace menu_backend.Services;

/// <summary>
/// Every 15 minutes, sends each restaurant's daily restock reminder once it is past 9 AM local
/// (at most one per restaurant per day).
/// </summary>
public class NotificationReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationReminderWorker> _logger;

    public NotificationReminderWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var sent = await notifications.SendDailyStockRemindersAsync();
                if (sent > 0)
                    _logger.LogInformation("[Notifications] Sent restock reminders to {Count} restaurants", sent);

                // Walkie-talkie clips are kept 24 hours for replay
                var db = scope.ServiceProvider.GetRequiredService<menu_backend.Data.AppDbContext>();
                var cutoff = DateTime.UtcNow.AddHours(-24);
                var deletedClips = await db.VoiceMessages.IgnoreQueryFilters().Where(m => m.CreatedAt < cutoff).ExecuteDeleteAsync(stoppingToken);
                if (deletedClips > 0)
                    _logger.LogInformation("[Walkie] Deleted {Count} messages older than 24 hours", deletedClips);

                var expired = await scope.ServiceProvider.GetRequiredService<LoyaltyService>().ExpireAsync();
                if (expired > 0)
                    _logger.LogInformation("[Loyalty] Expired points of {Count} inactive customers", expired);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[Notifications] Restock reminder sweep failed");
            }
        }
    }
}
