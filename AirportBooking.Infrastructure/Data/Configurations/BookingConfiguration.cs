using AirportBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirportBooking.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Reference).HasMaxLength(12).IsRequired();
        builder.Property(b => b.Currency).HasMaxLength(3).IsRequired();
        builder.Property(b => b.ContactEmail).HasMaxLength(256).IsRequired();
        builder.Property(b => b.ContactPhone).HasMaxLength(32);
        builder.Property(b => b.TotalAmount).HasPrecision(10, 2);

        // Stored as text rather than an int: a migration that reorders the enum
        // can silently turn every Cancelled booking into a Confirmed one, and
        // "Confirmed" in a psql session beats decoding a 1.
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Restrict, not Cascade: a booking is a financial record. Deleting a
        // user must not silently erase what they paid for — close the account
        // and anonymise instead.
        builder.HasOne(b => b.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Flight)
            .WithMany()
            .HasForeignKey(b => b.FlightId)
            .OnDelete(DeleteBehavior.Restrict);

        // Passengers have no meaning outside their booking, so they go with it.
        builder.HasMany(b => b.Passengers)
            .WithOne(p => p.Booking)
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.Reference).IsUnique();

        // Covers "my bookings, newest first" without a sort step.
        builder.HasIndex(b => new { b.UserId, b.CreatedAtUtc });
        builder.HasIndex(b => b.Status);
    }
}
