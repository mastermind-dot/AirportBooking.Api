using AirportBooking.Domain.Entities;

namespace AirportBooking.Infrastructure.Data.Seed;

/// <summary>
/// Generates a rolling schedule of flights from a table of routes.
///
/// Two things make this useful rather than decorative: the window is relative
/// to today, so search always has future flights to return no matter when the
/// database was created; and prices vary by weekday and by how close departure
/// is, so the price filter and a cheapest-first sort have something real to
/// work on.
/// </summary>
internal static class FlightSeedData
{
    /// <summary>How far ahead to generate. A month exercises every filter.</summary>
    internal const int DaysAhead = 30;

    private static readonly Dictionary<string, string> Airlines = new()
    {
        ["SN"] = "Brussels Airlines",
        ["AF"] = "Air France",
        ["KL"] = "KLM Royal Dutch Airlines",
        ["BA"] = "British Airways",
        ["LH"] = "Lufthansa",
        ["IB"] = "Iberia",
        ["VY"] = "Vueling",
        ["FR"] = "Ryanair",
        ["TP"] = "TAP Air Portugal",
        ["LX"] = "SWISS",
        ["OS"] = "Austrian Airlines",
        ["SK"] = "SAS",
        ["TK"] = "Turkish Airlines",
        ["AZ"] = "ITA Airways",
        ["EI"] = "Aer Lingus",
        ["A3"] = "Aegean Airlines",
        ["EK"] = "Emirates",
        ["AT"] = "Royal Air Maroc",
        ["AC"] = "Air Canada",
        ["DL"] = "Delta Air Lines"
    };

    /// <param name="DepartureHours">Local departure hours at the origin airport, one daily flight each.</param>
    private sealed record Route(
        string Origin,
        string Destination,
        string Airline,
        int DurationMinutes,
        decimal BasePrice,
        int[] DepartureHours,
        int Stops = 0,
        int Seats = 180);

    // Every route also generates its return leg, so these 41 rows become 82
    // directional routes. Durations and fares are close enough to real ones
    // that the results look plausible in the UI.
    private static readonly Route[] Routes =
    [
        // Brussels short-haul
        new("BRU", "LHR", "SN",  80, 129, [7, 13, 18]),
        new("BRU", "CDG", "AF",  70, 119, [8, 17]),
        new("BRU", "AMS", "KL",  55,  99, [7, 12, 19]),
        new("BRU", "FRA", "LH",  75, 139, [6, 16]),
        new("BRU", "MUC", "LH",  90, 135, [8, 18]),
        new("BRU", "ZRH", "LX",  85, 145, [7, 17]),
        new("BRU", "VIE", "OS", 105, 139, [9, 18]),
        new("BRU", "CPH", "SK",  95, 129, [8, 17]),
        new("BRU", "ARN", "SK", 135, 155, [10]),
        new("BRU", "OSL", "SK", 130, 159, [11]),
        new("BRU", "DUB", "EI",  95, 115, [7, 16]),
        new("BRU", "LUX", "SN",  45,  89, [9]),

        // Brussels to Southern Europe
        new("BRU", "MAD", "IB", 145, 159, [9, 18]),
        new("BRU", "BCN", "VY", 135, 109, [10, 19]),
        new("BRU", "FCO", "AZ", 140, 149, [8, 16]),
        new("BRU", "MXP", "AZ", 110, 129, [7, 15]),
        new("BRU", "LIS", "TP", 175, 169, [11]),
        new("BRU", "ATH", "A3", 210, 179, [9]),

        // Charleroi low-cost
        new("CRL", "BCN", "FR", 130,  69, [6, 15]),
        new("CRL", "MAD", "FR", 145,  79, [7]),
        new("CRL", "FCO", "FR", 145,  75, [6, 16]),
        new("CRL", "RAK", "FR", 220,  89, [8]),

        // Brussels long-haul, direct
        new("BRU", "IST", "TK", 195, 189, [11, 21], Seats: 220),
        new("BRU", "CMN", "AT", 215, 239, [9],      Seats: 200),
        new("BRU", "DXB", "EK", 380, 449, [11, 21], Seats: 300),
        new("BRU", "JFK", "DL", 490, 549, [10],     Seats: 280),
        new("BRU", "YUL", "AC", 465, 519, [13],     Seats: 280),

        // Brussels long-haul with a connection, so the direct-only filter has
        // something to actually exclude.
        new("BRU", "JFK", "AF", 625, 429, [7], Stops: 1, Seats: 280),
        new("BRU", "DXB", "TK", 565, 339, [6], Stops: 1, Seats: 280),
        new("BRU", "ATH", "LH", 300, 129, [6], Stops: 1),
        new("BRU", "LIS", "IB", 330, 119, [7], Stops: 1),

        // Routes that never touch Belgium, so search is not Brussels-only
        new("AMS", "LHR", "BA",  80, 125, [7, 14, 19]),
        new("AMS", "JFK", "KL", 480, 529, [10],    Seats: 280),
        new("CDG", "JFK", "AF", 490, 559, [11],    Seats: 300),
        new("LHR", "JFK", "BA", 470, 579, [9, 16], Seats: 300),
        new("FRA", "DXB", "LH", 375, 469, [13],    Seats: 280),
        new("CDG", "BCN", "VY", 105,  89, [8, 18]),
        new("MAD", "LIS", "TP",  80,  89, [9, 19]),
        new("FCO", "ATH", "A3", 110,  99, [10]),
        new("LHR", "DUB", "EI",  85,  99, [8, 17]),
        new("ZRH", "FCO", "LX", 105, 129, [12])
    ];

    /// <summary>
    /// Builds every flight in the window. <paramref name="existingKeys"/> holds
    /// the flight-number/departure pairs already in the database, so a second
    /// run only tops up the days that have newly come into range.
    /// </summary>
    internal static List<Flight> Generate(
        IReadOnlyDictionary<string, Guid> airportIdsByIata,
        IReadOnlyDictionary<string, TimeZoneInfo> timeZonesByIata,
        HashSet<(string FlightNumber, DateTime DepartureTimeUtc)> existingKeys,
        DateOnly startDate)
    {
        // Fixed seed: two runs over the same window produce the same prices, so
        // a screenshot or a test expectation does not rot between runs.
        var random = new Random(20260101);
        var flights = new List<Flight>();

        for (var routeIndex = 0; routeIndex < Routes.Length; routeIndex++)
        {
            var route = Routes[routeIndex];

            // Skip a route whose airports are not in the seed set.
            if (!airportIdsByIata.ContainsKey(route.Origin) ||
                !airportIdsByIata.ContainsKey(route.Destination))
            {
                continue;
            }

            // Outbound numbers are even, return numbers odd, spaced 10 apart per
            // route so a route can carry up to five daily departures each way.
            var baseNumber = 1000 + (routeIndex * 10);

            for (var dayOffset = 0; dayOffset < DaysAhead; dayOffset++)
            {
                var date = startDate.AddDays(dayOffset);

                for (var hourIndex = 0; hourIndex < route.DepartureHours.Length; hourIndex++)
                {
                    var departureHour = route.DepartureHours[hourIndex];
                    var number = baseNumber + (hourIndex * 2);

                    AddLeg(route.Origin, route.Destination, number, departureHour);

                    // The return leg leaves later in the day, as it would in a
                    // real rotation: the aircraft has to fly out first.
                    AddLeg(route.Destination, route.Origin, number + 1, (departureHour + 4) % 24);
                }

                void AddLeg(string origin, string destination, int number, int localHour)
                {
                    var flightNumber = $"{route.Airline}{number}";

                    var departureUtc = ToUtc(
                        date,
                        localHour,
                        minute: routeIndex * 5 % 60,
                        timeZonesByIata[origin]);

                    // Already seeded on an earlier run, or a duplicate within
                    // this batch. Either way, skip it.
                    if (!existingKeys.Add((flightNumber, departureUtc)))
                    {
                        return;
                    }

                    flights.Add(new Flight(
                        flightNumber,
                        Airlines[route.Airline],
                        route.Airline,
                        airportIdsByIata[origin],
                        airportIdsByIata[destination],
                        departureUtc,
                        departureUtc.AddMinutes(route.DurationMinutes),
                        PriceFor(route.BasePrice, date, dayOffset, random),
                        route.Seats,
                        route.Stops));
                }
            }
        }

        // Sell seats up front so the results are not a wall of brand-new
        // flights. The two tails matter more than the bulk: without them the
        // "only 3 seats left" badge and the sold-out path never appear in
        // development, and both are easy to get wrong unseen.
        foreach (var flight in flights)
        {
            var sold = random.NextDouble() switch
            {
                < 0.02 => flight.TotalSeats,                            // sold out
                < 0.08 => flight.TotalSeats - random.Next(1, 10),       // nearly full
                _ => (int)(flight.TotalSeats * random.NextDouble() * 0.85)
            };

            if (sold > 0)
            {
                flight.ReserveSeats(sold);
            }
        }

        return flights;
    }

    /// <summary>
    /// Weekend travel and last-minute booking both cost more, which is what
    /// makes a price filter worth having in the first place.
    /// </summary>
    private static decimal PriceFor(decimal basePrice, DateOnly date, int dayOffset, Random random)
    {
        var multiplier = date.DayOfWeek switch
        {
            DayOfWeek.Friday or DayOfWeek.Sunday => 1.18m,
            DayOfWeek.Saturday => 1.08m,
            DayOfWeek.Tuesday or DayOfWeek.Wednesday => 0.92m,
            _ => 1.00m
        };

        multiplier *= dayOffset switch
        {
            < 7 => 1.25m,
            < 14 => 1.12m,
            _ => 1.00m
        };

        // Twelve percent of jitter either way, so two flights on the same route
        // and day are not priced identically.
        multiplier *= 0.88m + ((decimal)random.NextDouble() * 0.24m);

        return Math.Round(basePrice * multiplier, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Converts a local departure time at the origin airport into the UTC value
    /// that is stored in the database.
    /// </summary>
    private static DateTime ToUtc(DateOnly date, int hour, int minute, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute));

        // On the spring-forward date the local clock skips an hour, so a time
        // such as 02:30 does not exist and ConvertTimeToUtc would throw.
        if (timeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            timeZone);
    }
}
