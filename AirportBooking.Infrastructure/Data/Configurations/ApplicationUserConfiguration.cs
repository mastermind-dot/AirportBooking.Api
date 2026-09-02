using AirportBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirportBooking.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Identity's own table names are kept as-is (AspNetUsers, AspNetRoles, …)
        // so anyone who has seen an ASP.NET Core schema before recognises them.
        builder.Property(u => u.FirstName).HasMaxLength(64).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(64).IsRequired();
        builder.Property(u => u.PreferredLanguage).HasMaxLength(5).IsRequired();

        builder.Ignore(u => u.FullName);
    }
}
