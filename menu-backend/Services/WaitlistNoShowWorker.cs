using menu_backend.Services.Interfaces;

namespace menu_backend.Services;

/// <summary>
/// Every minute, marks called waitlist parties who never arrived as no-shows
/// (per restaurant's WaitlistNoShowMinutes; 0 turns it off), and expires reservations:
/// confirmed bookings past their grace period become no-shows, unanswered requests past their time are declined.
/// </summary>
public class WaitlistNoShowWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaitlistNoShowWorker> _logger;

    public WaitlistNoShowWorker(IServiceScopeFactory scopeFactory, ILogger<WaitlistNoShowWorker> logger)
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
                var waitlist = scope.ServiceProvider.GetRequiredService<IWaitlistService>();
                var expired = await waitlist.ExpireNoShowsAsync();
                if (expired > 0)
                    _logger.LogInformation("[Waitlist] Marked {Count} called parties as no-show", expired);

                var reservations = scope.ServiceProvider.GetRequiredService<IReservationService>();
                var expiredBookings = await reservations.ExpireAsync();
                if (expiredBookings > 0)
                    _logger.LogInformation("[Reservations] Expired {Count} bookings", expiredBookings);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[Waitlist] No-show sweep failed");
            }
        }
    }
}
