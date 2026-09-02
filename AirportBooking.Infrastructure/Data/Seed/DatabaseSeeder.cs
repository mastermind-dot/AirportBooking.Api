using AirportBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AirportBooking.Infrastructure.Data.Seed;

/// <summary>
/// Brings a development database up to date and fills it with airports and a
/// rolling month of flights.
///
/// Every step is idempotent: run it on an empty database and it creates
/// everything, run it again tomorrow and it adds only the day that has newly
/// come into range. That matters more than it sounds — a seeder you are afraid
/// to re-run is a seeder you stop running.
/// </summary>
public static class DatabaseSeeder
{
    private const int InsertBatchSize = 500;

    /// <summary>
    /// Applies pending migrations, then seeds. Call this from Development only:
    /// in production, migrations run as a deployment step, because two
    /// instances starting at once would race each other over the same schema.
    /// </summary>
    public static async Task MigrateAndSeedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseSeeder));

        await db.Database.MigrateAsync(cancellationToken);

        var airportsAdded = await SeedAirportsAsync(db, cancellationToken);
        var flightsAdded = await SeedFlightsAsync(db, logger, cancellationToken);

        if (airportsAdded == 0 && flightsAdded == 0)
        {
            logger.LogInformation("Seed data already up to date.");
            return;
        }

        logger.LogInformation(
            "Seeded {AirportCount} airport(s) and {FlightCount} flight(s).",
            airportsAdded,
            flightsAdded);
    }

    private static async Task<int> SeedAirportsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existingCodes = await db.Airports
            .Select(a => a.IataCode)
            .ToListAsync(cancellationToken);

        var known = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = AirportSeedData.Airports
            .Where(definition => !known.Contains(definition.Iata))
            .Select(definition => new Airport
            {
                Id = Guid.NewGuid(),
                IataCode = definition.Iata,
                Name = definition.Name,
                City = definition.City,
                CountryCode = definition.CountryCode,
                TimeZoneId = definition.TimeZoneId
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            return 0;
        }

        db.Airports.AddRange(toAdd);
        await db.SaveChangesAsync(cancellationToken);

        return toAdd.Count;
    }

    private static async Task<int> SeedFlightsAsync(
        AppDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var airports = await db.Airports.AsNoTracking().ToListAsync(cancellationToken);
        if (airports.Count == 0)
        {
            return 0;
        }

        var airportIds = airports.ToDictionary(a => a.IataCode, a => a.Id);
        var timeZones = airports.ToDictionary(a => a.IataCode, a => ResolveTimeZone(a, logger));

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Npgsql rejects a DateTime whose Kind is not Utc when comparing against
        // a "timestamp with time zone" column, so both bounds are pinned to Utc.
        var windowStart = DateTime.SpecifyKind(
            startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var windowEnd = DateTime.SpecifyKind(
            startDate.AddDays(FlightSeedData.DaysAhead + 2).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        // Only the window is loaded, not the whole table — this stays cheap as
        // past flights accumulate.
        var existing = await db.Flights
            .AsNoTracking()
            .Where(f => f.DepartureTimeUtc >= windowStart && f.DepartureTimeUtc < windowEnd)
            .Select(f => new { f.FlightNumber, f.DepartureTimeUtc })
            .ToListAsync(cancellationToken);

        var existingKeys = existing
            .Select(f => (f.FlightNumber, f.DepartureTimeUtc))
            .ToHashSet();

        var flights = FlightSeedData.Generate(airportIds, timeZones, existingKeys, startDate);
        if (flights.Count == 0)
        {
            return 0;
        }

        // Inserted in batches with the change tracker cleared between them.
        // Tracking several thousand entities at once makes each SaveChanges
        // progressively slower for no benefit here.
        foreach (var batch in flights.Chunk(InsertBatchSize))
        {
            db.Flights.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        return flights.Count;
    }

    /// <summary>
    /// Looks up an airport's IANA time zone. Containers built without ICU (or
    /// with InvariantGlobalization enabled) cannot resolve these, and the
    /// resulting exception at startup is hard to read — so fall back to UTC and
    /// say so instead.
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(Airport airport, ILogger logger)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(airport.TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning(
                "Time zone {TimeZoneId} for {Iata} could not be resolved; seeding that airport in UTC.",
                airport.TimeZoneId,
                airport.IataCode);

            return TimeZoneInfo.Utc;
        }
    }
}
