using AirportBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirportBooking.Infrastructure.Data.Configurations;

public class CharterRequestConfiguration : IEntityTypeConfiguration<CharterRequest>
{
    public void Configure(EntityTypeBuilder<CharterRequest> builder)
    {
        builder.ToTable("CharterRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reference).HasMaxLength(16).IsRequired();
        builder.Property(r => r.ContactName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.ContactEmail).HasMaxLength(256).IsRequired();
        builder.Property(r => r.ContactPhone).HasMaxLength(32);
        builder.Property(r => r.Company).HasMaxLength(128);

        // Free text, not airport ids: much of what the SD360 serves has no IATA
        // code, and forcing a lookup would block the enquiries worth having.
        builder.Property(r => r.Origin).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Destination).HasMaxLength(128).IsRequired();

        builder.Property(r => r.CargoDescription).HasMaxLength(512);
        builder.Property(r => r.Message).HasMaxLength(2000);
        builder.Property(r => r.Locale).HasMaxLength(5).IsRequired();

        builder.Property(r => r.CargoWeightKg).HasPrecision(10, 2);

        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.PreferredAircraft).HasConversion<string>().HasMaxLength(24).IsRequired();

        // SetNull, not Cascade: deleting an account must not erase the
        // commercial record of an enquiry the team may still be working.
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.Reference).IsUnique();

        // The operations queue: new enquiries first, oldest at the top.
        builder.HasIndex(r => new { r.Status, r.CreatedAtUtc });
        builder.HasIndex(r => r.ContactEmail);
    }
}
