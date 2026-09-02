using Microsoft.AspNetCore.Identity;

namespace AirportBooking.Domain.Entities;

/// <summary>
/// The account behind a booking. Inherits Identity's password hashing,
/// lockout and email-confirmation machinery — never store a password here
/// yourself; <see cref="IdentityUser{TKey}.PasswordHash"/> is managed by
/// UserManager (PBKDF2 + per-user salt).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>"en" or "fr" — drives Accept-Language defaults and email copy.</summary>
    public string PreferredLanguage { get; set; } = "en";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; private set; } = new List<Booking>();

    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    public string FullName => $"{FirstName} {LastName}";
}
