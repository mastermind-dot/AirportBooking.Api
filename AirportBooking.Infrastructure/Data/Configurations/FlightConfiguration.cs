using AirportBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirportBooking.Infrastructure.Data.Configurations;

public class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        builder.ToTable("Flights");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FlightNumber).HasMaxLength(8).IsRequired();
        builder.Property(f => f.AirlineName).HasMaxLength(96).IsRequired();
        builder.Property(f => f.AirlineIataCode).HasMaxLength(3).IsRequired();
        builder.Property(f => f.Currency).HasMaxLength(3).IsRequired();

        // numeric(10,2): never float for money — binary floating point cannot
        // represent 0.10 exactly, and fare totals would drift by cents.
        builder.Property(f => f.BasePrice).HasPrecision(10, 2);

        builder.HasOne(f => f.Origin)
            .WithMany(a => a.DepartingFlights)
            .HasForeignKey(f => f.OriginAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Destination)
            .WithMany(a => a.ArrivingFlights)
            .HasForeignKey(f => f.DestinationAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        // The composite index the search endpoint actually hits: route first,
        // then date. Column order matters — Postgres can use a leading subset
        // of these columns, but not a trailing one.
        builder.HasIndex(f => new { f.OriginAirportId, f.DestinationAirportId, f.DepartureTimeUtc });
        builder.HasIndex(f => f.DepartureTimeUtc);
        builder.HasIndex(f => f.AirlineIataCode);

        // Postgres keeps a hidden xmin column that changes on every UPDATE.
        // Using it as a concurrency token means two people booking the last
        // seat at the same moment produce a DbUpdateConcurrencyException for
        // the loser instead of an oversold flight — no extra column needed.
        //
        // Npgsql 10 dropped the UseXminAsConcurrencyToken() helper: the provider
        // now recognises any uint row-version property and maps it to xmin.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
