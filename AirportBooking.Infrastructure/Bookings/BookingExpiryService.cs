using AirportBooking.Application.Interfaces;
using AirportBooking.Infrastructure.Data;
using AirportBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AirportBooking.Infrastructure.Bookings;

/// <summary>
/// Expires bookings that were created but never paid for, returning their seats.
///
/// Every abandoned checkout otherwise removes seats from sale forever: the
/// booking stays Pending, no webhook ever arrives because no payment was
/// attempted, and nothing puts the inventory back. On a busy route that quietly
/// makes a flight unbookable while it is largely empty.
/// </summary>
public sealed class BookingExpiryService : IBookingExpiryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BookingExpiryService> _logger;

    public BookingExpiryService(AppDbContext db, ILogger<BookingExpiryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> ExpireStaleBookingsAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(olderThan);

        // Passengers must be loaded: PassengerCount is Passengers.Count, and
        // ReleaseSeats ignores a count of zero. Omitting this include cancels
        // the bookings but silently returns no seats — the exact leak this
        // service exists to prevent, failing quietly.
        var stale = await _db.Bookings
            .Include(b => b.Flight)
            .Include(b => b.Payment)
            .Include(b => b.Passengers)
            .Where(b => b.Status == BookingStatus.Pending && b.CreatedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        var expired = 0;

        foreach (var booking in stale)
        {
            // A payment that already succeeded means the webhook is in flight or
            // arrived out of order. Cancelling that booking would take the seats
            // from someone who has actually paid.
            if (booking.Payment is { Status: PaymentStatus.Succeeded })
            {
                _logger.LogWarning(
                    "Booking {Reference} is Pending but its payment succeeded; leaving it for the webhook.",
                    booking.Reference);
                continue;
            }

            booking.Cancel();
            booking.Flight.ReleaseSeats(booking.PassengerCount);
            expired++;
        }

        if (expired > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Expired {Count} unpaid booking(s) and released their seats.", expired);
        }

        return expired;
    }
}
