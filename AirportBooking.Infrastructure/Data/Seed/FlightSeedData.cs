using AirportBooking.Domain.Entities;
using AirportBooking.Infrastructure.Common;

namespace AirportBooking.Infrastructure.Data.Seed;

/// <summary>
/// The scheduled network: the Goma-based SD360 runs across eastern DRC.
///
/// Only these are sold as seats. Everything else Malu Aviation flies — the
/// Kinshasa G159, cargo, and any strip without a published schedule — is
/// charter, quoted by a person through CharterRequest rather than booked here.
///
/// The window is relative to today, so search always has future flights no
/// matter when the database was created, and each route flies on fixed weekdays
/// the way a real regional operator schedules: not everything runs daily.
/// </summary>
internal static class FlightSeedData
{
    internal const int DaysAhead = 45;

    private const string Operator = "Malu Aviation";

    /// <summary>ICAO designator; the company has no IATA two-letter code.</summary>
    private const string OperatorCode = "MLU";

    private const string Aircraft = "Short SD360";

    /// <summary>Both Goma SD360s are 30-seat aircraft.</summary>
    private const int Seats = 30;

    /// <summary>
    /// USD is the working currency for DRC domestic aviation, so it is the
    /// default. It is configurable because Stripe converts on every charge whose
    /// currency differs from the account's settlement currency, and that fee is
    /// avoidable by making the two match.
    /// </summary>
    internal const string DefaultCurrency = "USD";

    private sealed record Route(
        string Origin,
        string Destination,
        int FlightNumber,
        int DurationMinutes,
        decimal BasePrice,
        int[] DepartureHours,
        DayOfWeek[] Days);

    private static readonly DayOfWeek[] Daily =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday
    ];

    private static readonly DayOfWeek[] MonWedFri =
        [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];

    private static readonly DayOfWeek[] TueThuSat =
        [DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday];

    private static readonly DayOfWeek[] TueFri =
        [DayOfWeek.Tuesday, DayOfWeek.Friday];

    /// <summary>
    /// Every route generates its return leg, so these six become twelve. Fares
    /// are the base figure before the demand adjustment in PriceFor.
    /// </summary>
    private static readonly Route[] Routes =
    [
        // Bukavu is barely 100 km down the lake — the shortest, busiest hop.
        new("GOM", "BKY", 210, 30, 120, [7, 15], Daily),

        new("GOM", "BNC", 220, 55, 180, [8], Daily),
        new("GOM", "RUE", 230, 45, 155, [9], MonWedFri),
        new("GOM", "BUX", 240, 80, 220, [7], TueThuSat),
        new("GOM", "FKI", 250, 115, 320, [10], MonWedFri),
        new("GOM", "KND", 260, 95, 280, [11], TueFri)
    ];

    internal static List<Flight> Generate(
        IReadOnlyDictionary<string, Guid> airportIdsByIata,
        IReadOnlyDictionary<string, TimeZoneInfo> timeZonesByIata,
        HashSet<(string FlightNumber, DateTime DepartureTimeUtc)> existingKeys,
        DateOnly startDate,
        string currency = DefaultCurrency)
    {
        // Fixed seed: two runs over the same window produce the same prices, so
        // a screenshot or a test expectation does not rot between runs.
        var random = new Random(20260101);
        var flights = new List<Flight>();

        foreach (var route in Routes)
        {
            if (!airportIdsByIata.ContainsKey(route.Origin) ||
                !airportIdsByIata.ContainsKey(route.Destination))
            {
                continue;
            }

            for (var dayOffset = 0; dayOffset < DaysAhead; dayOffset++)
            {
                var date = startDate.AddDays(dayOffset);

                if (!route.Days.Contains(date.DayOfWeek))
                {
                    continue;
                }

                for (var hourIndex = 0; hourIndex < route.DepartureHours.Length; hourIndex++)
                {
                    var outboundHour = route.DepartureHours[hourIndex];
                    var number = route.FlightNumber + (hourIndex * 2);

                    AddLeg(route.Origin, route.Destination, number, outboundHour);

                    // The aircraft has to fly out before it can come back. A
                    // turnaround plus the sector time keeps the return plausible.
                    var turnaround = (route.DurationMinutes + 45) / 60 + 1;
                    AddLeg(route.Destination, route.Origin, number + 1, (outboundHour + turnaround) % 24);
                }

                void AddLeg(string origin, string destination, int number, int localHour)
                {
                    var flightNumber = $"{OperatorCode}{number}";

                    // Departure hours are local to the origin; AirportClock
                    // converts to the UTC instant stored on the row, and absorbs
                    // the DST gap where a local time does not exist.
                    var departureUtc = AirportClock.ToUtc(
                        date.ToDateTime(new TimeOnly(localHour, number % 4 * 15)),
                        timeZonesByIata[origin]);

                    if (!existingKeys.Add((flightNumber, departureUtc)))
                    {
                        return;
                    }

                    flights.Add(new Flight(
                        flightNumber,
                        Operator,
                        OperatorCode,
                        airportIdsByIata[origin],
                        airportIdsByIata[destination],
                        departureUtc,
                        departureUtc.AddMinutes(route.DurationMinutes),
                        PriceFor(route.BasePrice, date, dayOffset, random),
                        Seats,
                        stops: 0,
                        currency: currency,
                        aircraftType: Aircraft));
                }
            }
        }

        // Sell seats up front so results are not a wall of empty aircraft. The
        // two tails matter most: without them the "only 3 seats left" badge and
        // the sold-out path never appear in development.
        foreach (var flight in flights)
        {
            var sold = random.NextDouble() switch
            {
                < 0.03 => flight.TotalSeats,
                < 0.12 => flight.TotalSeats - random.Next(1, 6),
                _ => (int)(flight.TotalSeats * random.NextDouble() * 0.8)
            };

            if (sold > 0)
            {
                flight.ReserveSeats(sold);
            }
        }

        return flights;
    }

    /// <summary>
    /// Fares move with demand: weekend and Monday travel costs more, and the
    /// last week before departure carries a surcharge. Without some spread the
    /// price filter and a cheapest-first sort have nothing to work on.
    /// </summary>
    private static decimal PriceFor(decimal basePrice, DateOnly date, int dayOffset, Random random)
    {
        var multiplier = date.DayOfWeek switch
        {
            DayOfWeek.Friday or DayOfWeek.Monday => 1.15m,
            DayOfWeek.Saturday => 1.08m,
            DayOfWeek.Tuesday or DayOfWeek.Wednesday => 0.94m,
            _ => 1.00m
        };

        multiplier *= dayOffset switch
        {
            < 7 => 1.2m,
            < 14 => 1.1m,
            _ => 1.00m
        };

        multiplier *= 0.92m + ((decimal)random.NextDouble() * 0.16m);

        // Domestic fares are quoted in whole dollars, not cents.
        return Math.Round(basePrice * multiplier, 0, MidpointRounding.AwayFromZero);
    }
}
