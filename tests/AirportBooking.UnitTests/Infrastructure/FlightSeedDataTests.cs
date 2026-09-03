using AirportBooking.Domain.Entities;
using AirportBooking.Infrastructure.Common;
using AirportBooking.Infrastructure.Data.Seed;

namespace AirportBooking.UnitTests.Infrastructure;

/// <summary>
/// The seeder decides what customers can actually find and book. It is also
/// idempotent by design — it runs on every startup — so "adds nothing the
/// second time" is a correctness property, not a nicety.
/// </summary>
public class FlightSeedDataTests
{
    private static readonly string[] Iata =
        ["GOM", "NLO", "FIH", "BKY", "BNC", "RUE", "BUX", "FKI", "KND", "FBM", "MJM", "KGA", "FMI", "MDK", "MAT", "KGL", "EBB", "BJM"];

    /// <summary>A Monday, so weekday-dependent assertions have a fixed anchor.</summary>
    private static readonly DateOnly Monday = new(2026, 9, 7);

    private static (Dictionary<string, Guid> Ids, Dictionary<string, TimeZoneInfo> Zones) Airports()
    {
        var ids = Iata.ToDictionary(code => code, _ => Guid.NewGuid());

        var zones = Iata.ToDictionary(
            code => code,
            code => AirportClock.Resolve(code switch
            {
                "NLO" or "FIH" or "MDK" or "MAT" => "Africa/Kinshasa",
                "KGL" => "Africa/Kigali",
                "EBB" => "Africa/Kampala",
                "BJM" => "Africa/Bujumbura",
                _ => "Africa/Lubumbashi"
            }));

        return (ids, zones);
    }

    private static List<Flight> Generate(HashSet<(string, DateTime)>? keys = null, DateOnly? from = null)
    {
        var (ids, zones) = Airports();
        return FlightSeedData.Generate(ids, zones, keys ?? [], from ?? Monday);
    }

    [Fact]
    public void The_generated_schedule_is_not_empty()
    {
        Assert.NotEmpty(Generate());
    }

    [Fact]
    public void No_two_flights_share_a_number_and_departure_time()
    {
        // This pair is the natural key the seeder de-duplicates on, and the
        // unique index the database would reject a collision with.
        var duplicates = Generate()
            .GroupBy(f => (f.FlightNumber, f.DepartureTimeUtc))
            .Count(g => g.Count() > 1);

        Assert.Equal(0, duplicates);
    }

    [Fact]
    public void Running_the_seeder_again_adds_nothing()
    {
        // It runs on every startup in development. A second pass adding rows
        // would duplicate the entire schedule daily.
        var keys = new HashSet<(string, DateTime)>();

        var first = Generate(keys);
        var second = Generate(keys);

        Assert.NotEmpty(first);
        Assert.Empty(second);
    }

    [Fact]
    public void Every_flight_arrives_after_it_departs()
    {
        Assert.All(Generate(), f => Assert.True(f.ArrivalTimeUtc > f.DepartureTimeUtc));
    }

    [Fact]
    public void Every_flight_departs_within_the_generated_window()
    {
        var flights = Generate();
        var windowEnd = Monday.AddDays(FlightSeedData.DaysAhead + 1).ToDateTime(TimeOnly.MinValue);

        Assert.All(flights, f =>
        {
            Assert.True(f.DepartureTimeUtc >= Monday.ToDateTime(TimeOnly.MinValue).AddDays(-1));
            Assert.True(f.DepartureTimeUtc <= windowEnd);
        });
    }

    [Fact]
    public void Seat_counts_are_never_impossible()
    {
        Assert.All(Generate(), f =>
        {
            Assert.InRange(f.AvailableSeats, 0, f.TotalSeats);
        });
    }

    [Fact]
    public void Some_flights_are_nearly_full_and_some_are_sold_out()
    {
        // Without these tails, the "only 3 seats left" badge and the sold-out
        // path never appear in development and are first exercised in public.
        var flights = Generate();

        Assert.Contains(flights, f => f.AvailableSeats == 0);
        Assert.Contains(flights, f => f.AvailableSeats is > 0 and < 10);
    }

    [Fact]
    public void Fares_have_enough_spread_for_a_price_filter_to_mean_something()
    {
        var fares = Generate().Select(f => f.BasePrice).ToList();

        Assert.All(fares, fare => Assert.True(fare > 0));
        Assert.True(fares.Max() > fares.Min() * 1.5m);
    }

    [Fact]
    public void The_whole_scheduled_network_is_flown_by_the_Goma_SD360s()
    {
        // Scheduled seats are the SD360 runs. Everything else the company flies
        // is charter, and must not appear as bookable inventory.
        Assert.All(Generate(), f =>
        {
            Assert.Equal("Short SD360", f.AircraftType);
            Assert.Equal(30, f.TotalSeats);
            Assert.Equal("USD", f.Currency);
            Assert.Equal(0, f.Stops);
        });
    }

    [Fact]
    public void Routes_that_do_not_fly_on_a_given_weekday_produce_nothing_that_day()
    {
        // Bunia runs Tuesday, Thursday and Saturday. A schedule where every
        // route flies daily would be a generator that ignores its own table.
        var (ids, zones) = Airports();
        var flights = FlightSeedData.Generate(ids, zones, [], Monday);

        var gomaToBunia = flights
            .Where(f => f.OriginAirportId == ids["GOM"] && f.DestinationAirportId == ids["BUX"])
            .Select(f => AirportClock.ToLocal(f.DepartureTimeUtc, zones["GOM"]).DayOfWeek)
            .Distinct()
            .ToList();

        Assert.NotEmpty(gomaToBunia);
        Assert.DoesNotContain(DayOfWeek.Monday, gomaToBunia);
        Assert.DoesNotContain(DayOfWeek.Sunday, gomaToBunia);
    }

    [Fact]
    public void Nothing_is_scheduled_on_a_Sunday()
    {
        var (ids, zones) = Airports();
        var flights = FlightSeedData.Generate(ids, zones, [], Monday);

        var sundays = flights.Count(f =>
            AirportClock.ToLocal(f.DepartureTimeUtc, zones["GOM"]).DayOfWeek == DayOfWeek.Sunday);

        Assert.Equal(0, sundays);
    }

    [Fact]
    public void Every_route_is_flown_in_both_directions()
    {
        var (ids, zones) = Airports();
        var flights = FlightSeedData.Generate(ids, zones, [], Monday);

        var outbound = flights.Count(f => f.OriginAirportId == ids["GOM"] && f.DestinationAirportId == ids["BKY"]);
        var inbound = flights.Count(f => f.OriginAirportId == ids["BKY"] && f.DestinationAirportId == ids["GOM"]);

        Assert.True(outbound > 0);
        Assert.Equal(outbound, inbound);
    }

    [Fact]
    public void A_route_whose_airports_are_missing_is_skipped_rather_than_crashing()
    {
        // The seeder runs before anyone checks the airport list is complete.
        var partial = new Dictionary<string, Guid> { ["GOM"] = Guid.NewGuid(), ["BKY"] = Guid.NewGuid() };
        var zones = partial.ToDictionary(p => p.Key, _ => AirportClock.Resolve("Africa/Lubumbashi"));

        var flights = FlightSeedData.Generate(partial, zones, [], Monday);

        Assert.NotEmpty(flights);
        Assert.All(flights, f =>
            Assert.True(partial.ContainsValue(f.OriginAirportId) && partial.ContainsValue(f.DestinationAirportId)));
    }
}
