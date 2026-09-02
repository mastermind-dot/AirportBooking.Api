namespace AirportBooking.Domain.Entities;

/// <summary>
/// A departure or arrival point. Reference data — seeded, not user-created.
/// </summary>
public class Airport
{
    public Guid Id { get; set; }

    /// <summary>Three-letter IATA code, uppercase (BRU, CDG, JFK). Unique.</summary>
    public required string IataCode { get; set; }

    public required string Name { get; set; }

    public required string City { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code (BE, FR, US).</summary>
    public required string CountryCode { get; set; }

    /// <summary>
    /// IANA time zone (Europe/Brussels). Flight times are stored in UTC;
    /// this is what lets the UI show "departs 14:30 local" correctly.
    /// </summary>
    public required string TimeZoneId { get; set; }

    public ICollection<Flight> DepartingFlights { get; private set; } = new List<Flight>();

    public ICollection<Flight> ArrivingFlights { get; private set; } = new List<Flight>();
}
