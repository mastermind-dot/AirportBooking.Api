using AirportBooking.Domain.Entities;
using AirportBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AirportBooking.IntegrationTests;

/// <summary>
/// A throwaway Postgres database, created once per test run and dropped after.
///
/// It uses the local server rather than a container. Testcontainers would be
/// more portable, but it needs Docker, and the behaviour under test — the xmin
/// system column backing optimistic concurrency — is a real Postgres feature
/// that no in-memory or SQLite provider emulates. A real server is the
/// requirement; where it comes from is not.
///
/// The database name carries a GUID so parallel runs, or a run that crashed
/// before cleanup, never collide.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Shares the API project's secret store, so no connection string is duplicated.</summary>
    private const string UserSecretsId = "cbc3011a-c253-4ffe-8315-42c217f91e4a";

    private string _adminConnectionString = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;
    public string DatabaseName { get; private set; } = string.Empty;

    /// <summary>Null when the server is unreachable; tests skip rather than fail.</summary>
    public string? SkipReason { get; private set; }

    public Guid UserId { get; private set; }
    public Guid OriginId { get; private set; }
    public Guid DestinationId { get; private set; }

    public async Task InitializeAsync()
    {
        var configured = new ConfigurationBuilder()
            .AddUserSecrets(UserSecretsId)
            .Build()
            .GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(configured))
        {
            SkipReason = "No ConnectionStrings:Default in user-secrets.";
            return;
        }

        DatabaseName = $"malu_test_{Guid.NewGuid():N}";

        var builder = new NpgsqlConnectionStringBuilder(configured);
        _adminConnectionString = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" }
            .ConnectionString;
        builder.Database = DatabaseName;
        ConnectionString = builder.ConnectionString;

        try
        {
            await using var admin = new NpgsqlConnection(_adminConnectionString);
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{DatabaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            SkipReason = $"Postgres unreachable: {ex.Message}";
            return;
        }

        // Real migrations, not EnsureCreated: the xmin mapping and the unique
        // indexes are part of what is under test, and only migrations produce
        // the schema the application actually runs against.
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        await SeedAsync(db);
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    /// <summary>One user and one route, enough for every test to build on.</summary>
    private async Task SeedAsync(AppDbContext db)
    {
        UserId = Guid.NewGuid();

        db.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "integration@example.cd",
            Email = "integration@example.cd",
            NormalizedEmail = "INTEGRATION@EXAMPLE.CD",
            NormalizedUserName = "INTEGRATION@EXAMPLE.CD",
            FirstName = "Integration",
            LastName = "Test",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        var origin = new Airport
        {
            Id = Guid.NewGuid(), IataCode = "GOM", Name = "Goma International",
            City = "Goma", CountryCode = "CD", TimeZoneId = "Africa/Lubumbashi"
        };

        var destination = new Airport
        {
            Id = Guid.NewGuid(), IataCode = "BKY", Name = "Kavumu",
            City = "Bukavu", CountryCode = "CD", TimeZoneId = "Africa/Lubumbashi"
        };

        db.Airports.AddRange(origin, destination);
        await db.SaveChangesAsync();

        OriginId = origin.Id;
        DestinationId = destination.Id;
    }

    /// <summary>Creates a flight with a known number of seats for one test to contend over.</summary>
    public async Task<Guid> CreateFlightAsync(int seats, decimal fare = 154m)
    {
        await using var db = CreateContext();

        var flight = new Flight(
            flightNumber: $"MLU{Random.Shared.Next(100, 999)}",
            airlineName: "Malu Aviation",
            airlineIataCode: "MLU",
            originAirportId: OriginId,
            destinationAirportId: DestinationId,
            departureTimeUtc: DateTime.UtcNow.AddDays(7),
            arrivalTimeUtc: DateTime.UtcNow.AddDays(7).AddMinutes(30),
            basePrice: fare,
            totalSeats: seats);

        db.Flights.Add(flight);
        await db.SaveChangesAsync();

        return flight.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(DatabaseName) || SkipReason is not null)
        {
            return;
        }

        // Pooled connections keep the database "in use" and block the drop.
        NpgsqlConnection.ClearAllPools();

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
