using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Bookings;
using AirportBooking.Application.Interfaces;
using AirportBooking.Domain.Entities;
using AirportBooking.Domain.Enums;
using AirportBooking.Infrastructure.Common;
using AirportBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AirportBooking.Infrastructure.Bookings;

public sealed class BookingService : IBookingService
{
    /// <summary>
    /// How many times to retry when another booking wins the race for the same
    /// seats. Three is enough to ride out normal contention; beyond that the
    /// flight is genuinely filling up faster than this request can commit, and
    /// the honest answer is to tell the user rather than keep spinning.
    /// </summary>
    private const int MaxConcurrencyRetries = 3;

    /// <summary>
    /// Booking closes shortly before departure. Without this, a flight that has
    /// already left is still bookable by anyone holding its id — search filters
    /// departed flights out, but nothing stops a direct request.
    /// </summary>
    private static readonly TimeSpan BookingCutoff = TimeSpan.FromHours(2);

    private readonly AppDbContext _db;
    private readonly ILogger<BookingService> _logger;

    public BookingService(AppDbContext db, ILogger<BookingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<BookingDto>> CreateAsync(
        Guid userId,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var passengerCount = request.Passengers.Count;

        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            // Each attempt starts from a clean tracker. Without this, the booking
            // and passengers added during a failed attempt would still be tracked
            // and get inserted twice on the retry.
            _db.ChangeTracker.Clear();

            var flight = await _db.Flights
                .Include(f => f.Origin)
                .Include(f => f.Destination)
                .SingleOrDefaultAsync(f => f.Id == request.FlightId, cancellationToken);

            if (flight is null)
            {
                return Result<BookingDto>.Failure(Error.FlightNotFound);
            }

            if (flight.DepartureTimeUtc <= DateTime.UtcNow.Add(BookingCutoff))
            {
                return Result<BookingDto>.Failure(Error.FlightDeparted);
            }

            if (!flight.HasSeatsFor(passengerCount))
            {
                return Result<BookingDto>.Failure(Error.NotEnoughSeats(flight.AvailableSeats));
            }

            // The total comes from the flight's stored fare, never from the
            // request. This is the single line that makes price tampering
            // impossible rather than merely discouraged.
            var total = flight.TotalFor(request.Cabin, passengerCount);

            flight.ReserveSeats(passengerCount);

            var booking = new Booking(
                userId,
                flight.Id,
                request.Cabin,
                total,
                flight.Currency,
                request.ContactEmail.Trim(),
                string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim());

            foreach (var passenger in request.Passengers)
            {
                booking.AddPassenger(new Passenger
                {
                    FirstName = passenger.FirstName.Trim(),
                    LastName = passenger.LastName.Trim(),
                    DateOfBirth = passenger.DateOfBirth,
                    Nationality = passenger.Nationality.Trim().ToUpperInvariant(),
                    PassportNumber = passenger.PassportNumber.Trim().ToUpperInvariant(),
                    PassportExpiry = passenger.PassportExpiry
                });
            }

            _db.Bookings.Add(booking);

            try
            {
                // One SaveChanges, so the seat decrement, the booking and its
                // passengers commit together or not at all. EF wraps this in a
                // transaction on its own — an explicit one would additionally
                // have to cooperate with the connection retry strategy.
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Booking {Reference} created for user {UserId} on flight {FlightNumber} ({Passengers} passenger(s), {Total} {Currency}).",
                    booking.Reference, userId, flight.FlightNumber, passengerCount, total, flight.Currency);

                return Result<BookingDto>.Success(ToDto(booking, flight, payment: null));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Somebody else changed this flight row between our read and our
                // write, so xmin no longer matches and our seat count was
                // computed from stale data. Read it again and redo the check —
                // the seats may still be there, or may now be gone.
                _logger.LogWarning(
                    "Seat reservation conflict on flight {FlightId}, attempt {Attempt} of {Max}.",
                    request.FlightId, attempt, MaxConcurrencyRetries);
            }
        }

        return Result<BookingDto>.Failure(Error.SeatsUnavailable);
    }

    public async Task<Result<PagedResult<BookingSummaryDto>>> GetForUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Where(b => b.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return Result<PagedResult<BookingSummaryDto>>.Success(
                PagedResult<BookingSummaryDto>.Empty(page, pageSize));
        }

        // The passenger count is computed in SQL rather than by loading the
        // passengers. Booking.PassengerCount is Passengers.Count, so reading it
        // without an include would report zero for every row — and loading them
        // would drag passport numbers into memory for a list that never shows
        // them.
        var rows = await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BookingRow(
                b.Id,
                b.Reference,
                b.Status,
                b.CabinClass,
                b.TotalAmount,
                b.Currency,
                b.Passengers.Count,
                b.CreatedAtUtc,
                b.Flight.Id,
                b.Flight.FlightNumber,
                b.Flight.AirlineName,
                b.Flight.Origin.IataCode,
                b.Flight.Origin.City,
                b.Flight.Origin.TimeZoneId,
                b.Flight.Destination.IataCode,
                b.Flight.Destination.City,
                b.Flight.Destination.TimeZoneId,
                b.Flight.DepartureTimeUtc,
                b.Flight.ArrivalTimeUtc,
                b.Flight.Stops))
            .ToListAsync(cancellationToken);

        var items = rows.Select(ToSummaryDto).ToList();

        return Result<PagedResult<BookingSummaryDto>>.Success(
            new PagedResult<BookingSummaryDto>(items, page, pageSize, totalCount));
    }

    public async Task<Result<BookingDto>> GetByIdAsync(
        Guid userId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await LoadOwnedBookingAsync(userId, bookingId, track: false, cancellationToken);

        return booking is null
            ? Result<BookingDto>.Failure(Error.BookingNotFound)
            : Result<BookingDto>.Success(ToDto(booking, booking.Flight, booking.Payment));
    }

    public async Task<Result<BookingDto>> CancelAsync(
        Guid userId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            _db.ChangeTracker.Clear();

            var booking = await LoadOwnedBookingAsync(userId, bookingId, track: true, cancellationToken);

            if (booking is null)
            {
                return Result<BookingDto>.Failure(Error.BookingNotFound);
            }

            if (booking.Status is BookingStatus.Cancelled or BookingStatus.Failed)
            {
                return Result<BookingDto>.Failure(Error.BookingNotCancellable);
            }

            if (booking.Flight.DepartureTimeUtc <= DateTime.UtcNow)
            {
                return Result<BookingDto>.Failure(Error.BookingNotCancellable);
            }

            booking.Cancel();

            // Seats go back to inventory. This also touches the flight row, so it
            // races with concurrent bookings the same way creation does.
            booking.Flight.ReleaseSeats(booking.PassengerCount);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Booking {Reference} cancelled; {Seats} seat(s) released.",
                    booking.Reference, booking.PassengerCount);

                return Result<BookingDto>.Success(ToDto(booking, booking.Flight, booking.Payment));
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning(
                    "Concurrency conflict cancelling booking {BookingId}, attempt {Attempt}.",
                    bookingId, attempt);
            }
        }

        return Result<BookingDto>.Failure(Error.SeatsUnavailable);
    }

    /// <summary>
    /// Loads a booking only if it belongs to this user. The ownership predicate
    /// is part of the query rather than a check afterwards, so there is no window
    /// in which someone else's booking exists in memory.
    /// </summary>
    private async Task<Booking?> LoadOwnedBookingAsync(
        Guid userId,
        Guid bookingId,
        bool track,
        CancellationToken cancellationToken)
    {
        var query = _db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.Payment)
            .Include(b => b.Flight).ThenInclude(f => f.Origin)
            .Include(b => b.Flight).ThenInclude(f => f.Destination)
            .Where(b => b.Id == bookingId && b.UserId == userId);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private static BookingDto ToDto(Booking booking, Flight flight, Payment? payment) =>
        new(booking.Id,
            booking.Reference,
            booking.Status,
            booking.CabinClass,
            booking.TotalAmount,
            booking.Currency,
            booking.ContactEmail,
            booking.ContactPhone,
            booking.CreatedAtUtc,
            booking.ConfirmedAtUtc,
            booking.CancelledAtUtc,
            ToFlightDto(flight),
            booking.Passengers
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .Select(p => new PassengerDto(
                    p.Id,
                    p.FirstName,
                    p.LastName,
                    p.DateOfBirth,
                    p.Nationality,
                    p.PassportNumber,
                    p.PassportExpiry,
                    p.SeatNumber))
                .ToList(),
            payment?.Status);

    /// <summary>The list view's columns, flattened so the query stays a single projection.</summary>
    private sealed record BookingRow(
        Guid Id,
        string Reference,
        BookingStatus Status,
        CabinClass Cabin,
        decimal TotalAmount,
        string Currency,
        int PassengerCount,
        DateTime CreatedAtUtc,
        Guid FlightId,
        string FlightNumber,
        string AirlineName,
        string OriginIata,
        string OriginCity,
        string OriginTimeZoneId,
        string DestinationIata,
        string DestinationCity,
        string DestinationTimeZoneId,
        DateTime DepartureTimeUtc,
        DateTime ArrivalTimeUtc,
        int Stops);

    private static BookingSummaryDto ToSummaryDto(BookingRow row)
    {
        var originZone = AirportClock.Resolve(row.OriginTimeZoneId);
        var destinationZone = AirportClock.Resolve(row.DestinationTimeZoneId);

        return new BookingSummaryDto(
            row.Id,
            row.Reference,
            row.Status,
            row.Cabin,
            row.TotalAmount,
            row.Currency,
            row.PassengerCount,
            row.CreatedAtUtc,
            new BookingFlightDto(
                row.FlightId,
                row.FlightNumber,
                row.AirlineName,
                row.OriginIata,
                row.OriginCity,
                row.DestinationIata,
                row.DestinationCity,
                row.DepartureTimeUtc,
                AirportClock.ToLocal(row.DepartureTimeUtc, originZone),
                row.ArrivalTimeUtc,
                AirportClock.ToLocal(row.ArrivalTimeUtc, destinationZone),
                (int)(row.ArrivalTimeUtc - row.DepartureTimeUtc).TotalMinutes,
                row.Stops));
    }

    private static BookingFlightDto ToFlightDto(Flight flight)
    {
        var originZone = AirportClock.Resolve(flight.Origin.TimeZoneId);
        var destinationZone = AirportClock.Resolve(flight.Destination.TimeZoneId);

        return new BookingFlightDto(
            flight.Id,
            flight.FlightNumber,
            flight.AirlineName,
            flight.Origin.IataCode,
            flight.Origin.City,
            flight.Destination.IataCode,
            flight.Destination.City,
            flight.DepartureTimeUtc,
            AirportClock.ToLocal(flight.DepartureTimeUtc, originZone),
            flight.ArrivalTimeUtc,
            AirportClock.ToLocal(flight.ArrivalTimeUtc, destinationZone),
            (int)flight.Duration.TotalMinutes,
            flight.Stops);
    }
}
