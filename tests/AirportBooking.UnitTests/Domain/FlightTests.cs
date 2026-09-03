using AirportBooking.Domain.Entities;
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
    [InlineData(1, 154)]
    [InlineData(2, 308)]
    [InlineData(9, 1386)]
    public void TotalFor_multiplies_the_fare_by_the_passenger_count(int passengers, decimal expected)
    {
        Assert.Equal(expected, Sd360().TotalFor(passengers));
    }

    [Fact]
    public void TotalFor_rejects_a_booking_with_no_passengers()
    {
        Assert.Throws<DomainException>(() => Sd360().TotalFor(0));
    }

    [Fact]
    public void The_fare_the_search_filters_on_is_the_fare_the_customer_pays()
    {
        // Search filters and sorts on BasePrice in SQL; the booking charges
        // TotalFor. With one cabin these are the same number, and this test is
        // what keeps them that way if a fare concept is ever reintroduced.
        var flight = Sd360(basePrice: 199m);

        Assert.Equal(flight.BasePrice, flight.TotalFor(1));
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
