using AirportBooking.Domain.Entities;
using AirportBooking.Domain.Enums;
using AirportBooking.Domain.Exceptions;

namespace AirportBooking.UnitTests.Domain;

/// <summary>
/// Flight owns the fare and the seat inventory — the two numbers that decide
/// what a customer is charged and whether a seat exists to sell. Both are
/// tested against exact values rather than "greater than zero", because an
/// off-by-a-cent or an off-by-one here is real money and a real oversold seat.
/// </summary>
public class FlightTests
{
    private static Flight Sd360(decimal basePrice = 154m, int seats = 30) =>
        new(
            flightNumber: "MLU210",
            airlineName: "Malu Aviation",
            airlineIataCode: "MLU",
            originAirportId: Guid.NewGuid(),
            destinationAirportId: Guid.NewGuid(),
            departureTimeUtc: new DateTime(2026, 9, 7, 5, 30, 0, DateTimeKind.Utc),
            arrivalTimeUtc: new DateTime(2026, 9, 7, 6, 0, 0, DateTimeKind.Utc),
            basePrice: basePrice,
            totalSeats: seats);

    // ---------- pricing ----------

    [Theory]
    [InlineData(CabinClass.Economy, 154.00)]
    [InlineData(CabinClass.PremiumEconomy, 246.40)]
    [InlineData(CabinClass.Business, 431.20)]
    [InlineData(CabinClass.First, 693.00)]
    public void PriceFor_applies_the_cabin_multiplier(CabinClass cabin, decimal expected)
    {
        Assert.Equal(expected, Sd360().PriceFor(cabin));
    }

    [Fact]
    public void PriceFor_rounds_to_cents_away_from_zero()
    {
        // 100.005 * 1.6 = 160.008 -> 160.01, not 160.00. Banker's rounding would
        // give the customer a cent here and take one somewhere else; fares are
        // quoted, so they round the same way every time.
        var flight = Sd360(basePrice: 100.005m);

        Assert.Equal(160.01m, flight.PriceFor(CabinClass.PremiumEconomy));
    }

    [Fact]
    public void TotalFor_multiplies_the_rounded_fare_by_the_passenger_count()
    {
        // Rounding happens per passenger, then multiplies. The alternative —
        // rounding the total — drifts from what each passenger is quoted.
        Assert.Equal(431.20m * 2, Sd360().TotalFor(CabinClass.Business, 2));
    }

    [Fact]
    public void TotalFor_rejects_a_booking_with_no_passengers()
    {
        Assert.Throws<DomainException>(() => Sd360().TotalFor(CabinClass.Economy, 0));
    }

    [Fact]
    public void MultiplierFor_matches_PriceFor_so_SQL_filtering_agrees_with_display()
    {
        // Search filters on BasePrice * MultiplierFor(cabin) in SQL, while the
        // page shows PriceFor(cabin). If these ever diverge, a flight can be
        // filtered in and then displayed at a price outside the filter.
        var flight = Sd360(basePrice: 199m);

        foreach (var cabin in Enum.GetValues<CabinClass>())
        {
            var viaMultiplier = Math.Round(199m * Flight.MultiplierFor(cabin), 2, MidpointRounding.AwayFromZero);
            Assert.Equal(flight.PriceFor(cabin), viaMultiplier);
        }
    }

    // ---------- seat inventory ----------

    [Fact]
    public void A_new_flight_has_every_seat_available()
    {
        var flight = Sd360(seats: 30);

        Assert.Equal(30, flight.TotalSeats);
        Assert.Equal(30, flight.AvailableSeats);
    }

    [Fact]
    public void ReserveSeats_takes_them_out_of_inventory()
    {
        var flight = Sd360(seats: 30);

        flight.ReserveSeats(4);

        Assert.Equal(26, flight.AvailableSeats);
    }

    [Fact]
    public void ReserveSeats_refuses_to_oversell()
    {
        var flight = Sd360(seats: 3);

        var error = Assert.Throws<DomainException>(() => flight.ReserveSeats(4));

        // The message reaches the customer, so it should say how many are left.
        Assert.Contains("3", error.Message);
        Assert.Equal(3, flight.AvailableSeats);
    }

    [Fact]
    public void ReserveSeats_can_take_exactly_the_last_seats()
    {
        var flight = Sd360(seats: 3);

        flight.ReserveSeats(3);

        Assert.Equal(0, flight.AvailableSeats);
        Assert.False(flight.HasSeatsFor(1));
    }

    [Fact]
    public void ReleaseSeats_never_returns_more_than_capacity()
    {
        // A double-delivered webhook, or a cancel racing an expiry sweep, must
        // not invent seats the aircraft does not have.
        var flight = Sd360(seats: 30);
        flight.ReserveSeats(2);

        flight.ReleaseSeats(2);
        flight.ReleaseSeats(2);

        Assert.Equal(30, flight.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeats_ignores_a_count_of_zero()
    {
        // This is the shape of the bug that made the expiry sweep silently
        // release nothing: PassengerCount was 0 because passengers were not
        // loaded, and the call became a no-op.
        var flight = Sd360(seats: 30);
        flight.ReserveSeats(5);

        flight.ReleaseSeats(0);

        Assert.Equal(25, flight.AvailableSeats);
    }

    // ---------- construction invariants ----------

    [Fact]
    public void A_flight_cannot_arrive_before_it_departs()
    {
        Assert.Throws<DomainException>(() => new Flight(
            "MLU210", "Malu Aviation", "MLU", Guid.NewGuid(), Guid.NewGuid(),
            departureTimeUtc: new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc),
            arrivalTimeUtc: new DateTime(2026, 9, 7, 7, 0, 0, DateTimeKind.Utc),
            basePrice: 100m, totalSeats: 30));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_flight_needs_a_positive_fare(decimal basePrice)
    {
        Assert.Throws<DomainException>(() => Sd360(basePrice: basePrice));
    }

    [Fact]
    public void A_flight_needs_at_least_one_seat()
    {
        Assert.Throws<DomainException>(() => Sd360(seats: 0));
    }

    [Fact]
    public void Duration_and_IsDirect_describe_the_sector()
    {
        var flight = Sd360();

        Assert.Equal(TimeSpan.FromMinutes(30), flight.Duration);
        Assert.True(flight.IsDirect);
    }
}
