using System.Security.Cryptography;
using AirportBooking.Domain.Enums;
using AirportBooking.Domain.Exceptions;

namespace AirportBooking.Domain.Entities;

/// <summary>
/// A user's reservation on one flight for one or more passengers.
/// Status transitions are methods rather than a public setter, because
/// "Confirmed" must only ever be reachable from a verified webhook.
/// </summary>
public class Booking
{
    // Ambiguous characters (0/O, 1/I) are excluded so a reference read over
    // the phone or off a screen can't be mistyped.
    private const string ReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private Booking() { } // EF Core

    public Booking(
        Guid userId,
        Guid flightId,
        CabinClass cabinClass,
        decimal totalAmount,
        string currency,
        string contactEmail,
        string? contactPhone = null)
    {
        if (totalAmount <= 0)
            throw new DomainException("Booking total must be greater than zero.");

        Id = Guid.NewGuid();
        Reference = GenerateReference();
        UserId = userId;
        FlightId = flightId;
        CabinClass = cabinClass;
        TotalAmount = totalAmount;
        Currency = currency;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Status = BookingStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Human-readable locator shown to the user, e.g. "MLU-7QK4M2". Charter
    /// enquiries use "MLU-Q-" so the two are never confused on a phone call.
    /// Random rather than sequential so one reference never reveals another.
    /// </summary>
    public string Reference { get; private set; } = null!;

    public Guid UserId { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    public Guid FlightId { get; private set; }
    public Flight Flight { get; private set; } = null!;

    public CabinClass CabinClass { get; private set; }

    public BookingStatus Status { get; private set; }

    /// <summary>Computed server-side from the flight fare. Never set from a request body.</summary>
    public decimal TotalAmount { get; private set; }

    public string Currency { get; private set; } = "EUR";

    public string ContactEmail { get; private set; } = null!;

    public string? ContactPhone { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    public ICollection<Passenger> Passengers { get; private set; } = new List<Passenger>();

    public Payment? Payment { get; private set; }

    public int PassengerCount => Passengers.Count;

    public void AddPassenger(Passenger passenger)
    {
        if (Status != BookingStatus.Pending)
            throw new DomainException("Passengers can only be added while a booking is pending.");

        Passengers.Add(passenger);
    }

    /// <summary>Called only after a Stripe webhook signature has been verified.</summary>
    public void MarkConfirmed()
    {
        if (Status == BookingStatus.Confirmed) return; // webhooks are delivered at-least-once
        if (Status != BookingStatus.Pending)
            throw new DomainException($"A {Status.ToString().ToLowerInvariant()} booking cannot be confirmed.");

        Status = BookingStatus.Confirmed;
        ConfirmedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        if (Status is BookingStatus.Cancelled or BookingStatus.Confirmed)
            throw new DomainException($"A {Status.ToString().ToLowerInvariant()} booking cannot be marked failed.");

        Status = BookingStatus.Failed;
    }

    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled) return;
        if (Status == BookingStatus.Failed)
            throw new DomainException("A failed booking cannot be cancelled.");

        Status = BookingStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
    }

    public void AttachPayment(Payment payment) => Payment = payment;

    /// <summary>Six random characters from a 32-symbol alphabet — about 10^9 combinations.</summary>
    private static string GenerateReference()
    {
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = ReferenceAlphabet[RandomNumberGenerator.GetInt32(ReferenceAlphabet.Length)];

        return $"MLU-{new string(chars)}";
    }
}
