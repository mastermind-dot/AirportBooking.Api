using AirportBooking.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AirportBooking.Infrastructure.Data;

/// <summary>
/// The single EF Core context. Inherits the Identity schema (AspNetUsers,
/// AspNetRoles, AspNetUserClaims, …) keyed by Guid, and adds the booking tables.
///
/// Entity shape is configured in Data/Configurations rather than here, so this
/// file stays readable as the model grows.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Must run first — it builds the Identity tables that the
        // configurations below then extend.
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        base.ConfigureConventions(builder);

        // Any decimal that escapes an explicit HasPrecision still lands as
        // numeric(10,2) rather than Postgres' unconstrained numeric.
        builder.Properties<decimal>().HavePrecision(10, 2);
    }
}
