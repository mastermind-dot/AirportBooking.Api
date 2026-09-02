using AirportBooking.Application.Interfaces;

namespace AirportBooking.Api.BackgroundServices;

/// <summary>
/// Periodically releases seats held by bookings that were never paid for.
///
/// Hosting lives in the API project because that is where the host is; the work
/// itself is in Infrastructure. This class only decides when it runs.
/// </summary>
public sealed class PendingBookingExpiryWorker : BackgroundService
{
    /// <summary>
    /// How long a booking may sit unpaid. Long enough to fill in passenger
    /// details and find a card, short enough that an abandoned checkout does not
    /// hold seats through a busy afternoon.
    /// </summary>
    private static readonly TimeSpan UnpaidBookingLifetime = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingBookingExpiryWorker> _logger;

    public PendingBookingExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingBookingExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // A fresh scope per sweep: the service depends on a scoped
                // DbContext, and holding one for the process lifetime would
                // accumulate tracked entities and stale data indefinitely.
                using var scope = _scopeFactory.CreateScope();

                var expiry = scope.ServiceProvider.GetRequiredService<IBookingExpiryService>();
                await expiry.ExpireStaleBookingsAsync(UnpaidBookingLifetime, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Swallowed on purpose: an unhandled exception here would end the
                // worker for the life of the process, so a transient database
                // blip would silently stop seats ever being released again.
                _logger.LogError(ex, "Booking expiry sweep failed; will retry next interval.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
