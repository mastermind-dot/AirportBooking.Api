using System.Security.Cryptography;
using AirportBooking.Domain.Enums;
using AirportBooking.Domain.Exceptions;

namespace AirportBooking.Domain.Entities;

/// <summary>
/// A request for a charter quote.
///
/// Deliberately not a Booking: nothing is reserved, no seats move, and no price
/// exists yet. Charter pricing depends on routing, permits, fuel uplift and
/// ground handling at strips that may have none of it, so it is quoted by a
/// person rather than computed. This entity only carries the enquiry to them.
///
/// Origin and destination are free text, not airport ids, because much of what
/// Malu Aviation serves has no IATA code — the whole point of the SD360 is
/// reaching strips that scheduled traffic does not.
/// </summary>
public class CharterRequest
{
    private const string ReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private CharterRequest() { } // EF Core

    public CharterRequest(
        CharterKind kind,
        string contactName,
        string contactEmail,
        string? contactPhone,
        string? company,
        string origin,
        string destination,
        DateOnly departureDate,
        DateOnly? returnDate,
        AircraftPreference preferredAircraft,
        int? passengerCount,
        decimal? cargoWeightKg,
        string? cargoDescription,
        string? message,
        string locale,
        Guid? userId)
    {
        if (returnDate is { } back && back < departureDate)
            throw new DomainException("The return date cannot be before the departure date.");

        if (kind == CharterKind.Passenger && passengerCount is null or < 1)
            throw new DomainException("A passenger charter needs a passenger count.");

        if (kind == CharterKind.Cargo && cargoWeightKg is null or <= 0)
            throw new DomainException("A cargo charter needs a weight.");

        Id = Guid.NewGuid();
        Reference = GenerateReference();
        Kind = kind;
        ContactName = contactName;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Company = company;
        Origin = origin;
        Destination = destination;
        DepartureDate = departureDate;
        ReturnDate = returnDate;
        PreferredAircraft = preferredAircraft;
        PassengerCount = passengerCount;
        CargoWeightKg = cargoWeightKg;
        CargoDescription = cargoDescription;
        Message = message;
        Locale = locale;
        UserId = userId;
        Status = CharterRequestStatus.New;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    /// <summary>Quoted back to the customer, e.g. "MLU-Q-4K7M2X".</summary>
    public string Reference { get; private set; } = null!;

    public CharterKind Kind { get; private set; }
    public CharterRequestStatus Status { get; private set; }

    public string ContactName { get; private set; } = null!;
    public string ContactEmail { get; private set; } = null!;
    public string? ContactPhone { get; private set; }
    public string? Company { get; private set; }

    public string Origin { get; private set; } = null!;
    public string Destination { get; private set; } = null!;

    public DateOnly DepartureDate { get; private set; }
    public DateOnly? ReturnDate { get; private set; }

    public AircraftPreference PreferredAircraft { get; private set; }

    public int? PassengerCount { get; private set; }
    public decimal? CargoWeightKg { get; private set; }
    public string? CargoDescription { get; private set; }

    public string? Message { get; private set; }

    /// <summary>"fr" or "en" — which language to answer in.</summary>
    public string Locale { get; private set; } = "fr";

    /// <summary>
    /// Null for anonymous enquiries. Requiring an account to ask for a price
    /// would lose most of them.
    /// </summary>
    public Guid? UserId { get; private set; }
    public ApplicationUser? User { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }

    public bool IsRoundTrip => ReturnDate is not null;

    public void MarkContacted()
    {
        if (Status == CharterRequestStatus.Closed)
            throw new DomainException("A closed request cannot be reopened this way.");

        Status = CharterRequestStatus.Contacted;
        RespondedAtUtc ??= DateTime.UtcNow;
    }

    public void MarkQuoted()
    {
        if (Status == CharterRequestStatus.Closed)
            throw new DomainException("A closed request cannot be quoted.");

        Status = CharterRequestStatus.Quoted;
        RespondedAtUtc ??= DateTime.UtcNow;
    }

    public void Close() => Status = CharterRequestStatus.Closed;

    private static string GenerateReference()
    {
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = ReferenceAlphabet[RandomNumberGenerator.GetInt32(ReferenceAlphabet.Length)];

        return $"MLU-Q-{new string(chars)}";
    }
}
