using AirportBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirportBooking.Infrastructure.Data.Configurations;

public class PassengerConfiguration : IEntityTypeConfiguration<Passenger>
{
    public void Configure(EntityTypeBuilder<Passenger> builder)
    {
        builder.ToTable("Passengers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.FirstName).HasMaxLength(64).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Nationality).HasMaxLength(2).IsRequired();
        builder.Property(p => p.PassportNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.SeatNumber).HasMaxLength(4);

        // DateOnly maps to Postgres "date" — no time zone conversion applied,
        // which is what you want for a birth date.
        builder.Property(p => p.DateOfBirth).IsRequired();

        builder.HasIndex(p => p.BookingId);
    }
}
