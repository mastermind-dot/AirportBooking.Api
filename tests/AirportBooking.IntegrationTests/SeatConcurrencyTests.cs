using AirportBooking.Application.DTOs.Bookings;
using AirportBooking.Domain.Entities;
using AirportBooking.Domain.Enums;
using AirportBooking.Infrastructure.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AirportBooking.IntegrationTests;

/// <summary>
/// The xmin concurrency guard, exercised against real Postgres.
///
/// This is the one piece of the system that cannot be unit-tested: xmin is a
/// Postgres system column, and the failure it prevents — two bookings reading
/// the same seat count and both writing — only happens when two transactions
/// genuinely overlap. An in-memory provider would pass these tests while the
/// production database oversold every flight.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SeatConcurrencyTests(PostgresFixture db)
{
    /// <summary>Skips rather than fails when no Postgres is reachable.</summary>
    private void RequireDatabase() =>
        Skip.If(db.SkipReason is not null, db.SkipReason ?? string.Empty);

    private BookingService NewService() =>
        new(db.CreateContext(), NullLogger<BookingService>.Instance);

    private static CreateBookingRequest OnePassenger(Guid flightId, int n) =>
        new(flightId, $"racer{n}@example.cd", null,
            [new PassengerRequest("Racer", $"N{n}", new DateOnly(1990, 1, 1), "CD", $"CD{n:D7}", null)]);

    [SkippableFact]
    public async Task Concurrent_bookings_never_oversell_a_flight()
    {
        RequireDatabase();

        const int seats = 5;
        const int racers = 25;

        var flightId = await db.CreateFlightAsync(seats);

        // Every task gets its own DbContext, as a real request would. Sharing
        // one would serialise them through its change tracker and prove nothing.
        var attempts = Enumerable.Range(0, racers).Select(async n =>
        {
            var service = NewService();
            return await service.CreateAsync(db.UserId, OnePassenger(flightId, n));
        });

        var results = await Task.WhenAll(attempts);

        var succeeded = results.Count(r => r.IsSuccess);
        var failed = results.Count(r => r.IsFailure);

        await using var check = db.CreateContext();
        var flight = await check.Flights.SingleAsync(f => f.Id == flightId);
        var bookings = await check.Bookings.CountAsync(b => b.FlightId == flightId);

        // The whole point: 25 racers, 5 seats, and the aircraft does not grow.
        Assert.Equal(seats, succeeded);
        Assert.Equal(racers - seats, failed);
        Assert.Equal(seats, bookings);
        Assert.Equal(0, flight.AvailableSeats);
        Assert.InRange(flight.AvailableSeats, 0, flight.TotalSeats);
    }

    [SkippableFact]
    public async Task Concurrent_bookings_for_several_seats_each_still_balance()
    {
        RequireDatabase();

        // Multi-seat bookings are the harder case: a partial reservation would
        // leave the flight sold but the booking short.
        const int seats = 12;
        var flightId = await db.CreateFlightAsync(seats);

        var attempts = Enumerable.Range(0, 10).Select(async n =>
        {
            var service = NewService();
            var passengers = Enumerable.Range(0, 3)
                .Select(p => new PassengerRequest(
                    "Group", $"N{n}{p}", new DateOnly(1990, 1, 1), "CD", $"GR{n}{p:D5}", null))
                .ToList();

            return await service.CreateAsync(db.UserId,
                new CreateBookingRequest(flightId, $"group{n}@example.cd", null, passengers));
        });

        var results = await Task.WhenAll(attempts);
        var succeeded = results.Count(r => r.IsSuccess);

        await using var check = db.CreateContext();
        var flight = await check.Flights.SingleAsync(f => f.Id == flightId);
        var passengersSold = await check.Passengers
            .CountAsync(p => p.Booking.FlightId == flightId);

        // Twelve seats, three per booking: at most four can win.
        Assert.InRange(succeeded, 1, 4);
        Assert.Equal(succeeded * 3, passengersSold);
        Assert.Equal(seats - (succeeded * 3), flight.AvailableSeats);
        Assert.True(flight.AvailableSeats >= 0);
    }

    [SkippableFact]
    public async Task A_cancellation_racing_bookings_returns_exactly_its_own_seats()
    {
        RequireDatabase();

        var flightId = await db.CreateFlightAsync(seats: 10);

        var first = await NewService().CreateAsync(db.UserId, OnePassenger(flightId, 100));
        Assert.True(first.IsSuccess);

        // Cancel while others are booking. Both paths write the flight row, so
        // both go through the same concurrency guard.
        var work = new List<Task>
        {
            NewService().CancelAsync(db.UserId, first.Value!.Id)
        };

        work.AddRange(Enumerable.Range(0, 8).Select(async n =>
            await NewService().CreateAsync(db.UserId, OnePassenger(flightId, 200 + n))));

        await Task.WhenAll(work);

        await using var check = db.CreateContext();
        var flight = await check.Flights.SingleAsync(f => f.Id == flightId);
        var live = await check.Bookings
            .Where(b => b.FlightId == flightId && b.Status != BookingStatus.Cancelled)
            .CountAsync();

        // Whatever the interleaving, seats held must equal live bookings.
        Assert.Equal(flight.TotalSeats - live, flight.AvailableSeats);
        Assert.InRange(flight.AvailableSeats, 0, flight.TotalSeats);
    }

    [SkippableFact]
    public async Task The_flight_row_actually_carries_an_xmin_concurrency_token()
    {
        RequireDatabase();

        // If the mapping were ever dropped, the tests above would still pass on
        // a fast machine and start overselling under load. This asserts the
        // mechanism itself, not just its effect.
        await using var context = db.CreateContext();

        var property = context.Model
            .FindEntityType(typeof(Flight))!
            .FindProperty("xmin");

        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal("xid", property.GetColumnType());
    }
}
