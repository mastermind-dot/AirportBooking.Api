using AirportBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirportBooking.Infrastructure.Data.Configurations;

public class AirportConfiguration : IEntityTypeConfiguration<Airport>
{
    public void Configure(EntityTypeBuilder<Airport> builder)
    {
        builder.ToTable("Airports");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.IataCode).HasMaxLength(3).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(128).IsRequired();
        builder.Property(a => a.City).HasMaxLength(96).IsRequired();
        builder.Property(a => a.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(a => a.TimeZoneId).HasMaxLength(64).IsRequired();

        // The airport picker looks airports up by code, and a duplicate code
        // would make "BRU" ambiguous for every search.
        builder.HasIndex(a => a.IataCode).IsUnique();
        builder.HasIndex(a => a.City);
    }
}
