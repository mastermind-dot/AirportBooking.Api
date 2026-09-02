using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Bookings;

namespace AirportBooking.Application.Interfaces;

/// <summary>
/// Every method takes the caller's user id and filters on it. Ownership is
/// enforced here rather than in the controller, so a new endpoint cannot expose
/// someone else's booking by forgetting the check.
/// </summary>
public interface IBookingService
{
    Task<Result<BookingDto>> CreateAsync(
        Guid userId,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<BookingSummaryDto>>> GetForUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<BookingDto>> GetByIdAsync(
        Guid userId,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels the booking and returns its seats to inventory.</summary>
    Task<Result<BookingDto>> CancelAsync(
        Guid userId,
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
