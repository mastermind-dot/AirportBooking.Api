namespace AirportBooking.Application.Interfaces;

/// <summary>
/// Releases seats held by bookings that were never paid for.
///
/// Without this, every abandoned checkout removes seats from sale permanently:
/// the booking sits Pending, no webhook ever arrives, and nothing puts the
/// inventory back.
/// </summary>
public interface IBookingExpiryService
{
    /// <returns>How many bookings were expired.</returns>
    Task<int> ExpireStaleBookingsAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
}
