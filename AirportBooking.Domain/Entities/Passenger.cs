namespace AirportBooking.Domain.Entities;

/// <summary>
/// A traveller on a booking. This is the most sensitive table in the schema —
/// passport numbers and dates of birth are personal data under GDPR, so it is
/// never returned to anyone but the booking's owner, and never written to logs.
/// </summary>
public class Passenger
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>Date only — a birth date has no time zone, so DateOnly avoids the off-by-one-day bugs a DateTime causes here.</summary>
    public required DateOnly DateOfBirth { get; set; }

    /// <summary>ISO 3166-1 alpha-2 nationality code.</summary>
    public required string Nationality { get; set; }

    /// <summary>
    /// Consider column-level encryption (pgcrypto, or EF Core value converters
    /// over a key from your vault) before this holds real passenger data.
    /// </summary>
    public required string PassportNumber { get; set; }

    public DateOnly? PassportExpiry { get; set; }

    /// <summary>Assigned at check-in, so null until then.</summary>
    public string? SeatNumber { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
