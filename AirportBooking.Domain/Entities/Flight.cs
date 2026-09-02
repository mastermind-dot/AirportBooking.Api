using AirportBooking.Domain.Enums;
using AirportBooking.Domain.Exceptions;

namespace AirportBooking.Domain.Entities;

/// <summary>
/// A scheduled flight and the single source of truth for its price.
/// The API never accepts an amount from the client — it calls
/// <see cref="PriceFor"/> here and multiplies by the passenger count.
/// </summary>
public class Flight
{
    // Cabin pricing is derived from the economy fare rather than stored per
    // cabin, so a fare change can never leave the four prices inconsistent.
    private static readonly Dictionary<CabinClass, decimal> CabinMultipliers = new()
    {
        [CabinClass.Economy] = 1.0m,
        [CabinClass.PremiumEconomy] = 1.6m,
        [CabinClass.Business] = 2.8m,
        [CabinClass.First] = 4.5m
    };

    private Flight() { } // EF Core

    public Flight(
        string flightNumber,
        string airlineName,
        string airlineIataCode,
        Guid originAirportId,
        Guid destinationAirportId,
        DateTime departureTimeUtc,
        DateTime arrivalTimeUtc,
        decimal basePrice,
        int totalSeats,
        int stops = 0,
        string currency = "EUR")
    {
        if (arrivalTimeUtc <= departureTimeUtc)
            throw new DomainException("A flight cannot arrive before it departs.");
        if (basePrice <= 0)
            throw new DomainException("Flight price must be greater than zero.");
        if (totalSeats <= 0)
            throw new DomainException("A flight must have at least one seat.");

        Id = Guid.NewGuid();
        FlightNumber = flightNumber;
        AirlineName = airlineName;
        AirlineIataCode = airlineIataCode;
        OriginAirportId = originAirportId;
        DestinationAirportId = destinationAirportId;
        DepartureTimeUtc = departureTimeUtc;
        ArrivalTimeUtc = arrivalTimeUtc;
        BasePrice = basePrice;
        Currency = currency;
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats;
        Stops = stops;
    }

    public Guid Id { get; private set; }

    /// <summary>Carrier flight number, e.g. "SN2103".</summary>
    public string FlightNumber { get; private set; } = null!;

    public string AirlineName { get; private set; } = null!;

    /// <summary>Two-character airline IATA code (SN, AF, BA) — used by the airline filter.</summary>
    public string AirlineIataCode { get; private set; } = null!;

    public Guid OriginAirportId { get; private set; }
    public Airport Origin { get; private set; } = null!;

    public Guid DestinationAirportId { get; private set; }
    public Airport Destination { get; private set; } = null!;

    /// <summary>Always UTC — Npgsql maps this to <c>timestamptz</c>.</summary>
    public DateTime DepartureTimeUtc { get; private set; }

    public DateTime ArrivalTimeUtc { get; private set; }

    /// <summary>Economy fare for one passenger. All other cabins derive from it.</summary>
    public decimal BasePrice { get; private set; }

    /// <summary>ISO 4217 code. Stripe wants the amount in minor units of this currency.</summary>
    public string Currency { get; private set; } = "EUR";

    public int TotalSeats { get; private set; }

    public int AvailableSeats { get; private set; }

    /// <summary>0 for direct flights. Drives the "direct only" filter.</summary>
    public int Stops { get; private set; }

    public TimeSpan Duration => ArrivalTimeUtc - DepartureTimeUtc;

    public bool IsDirect => Stops == 0;

    /// <summary>Fare for a single passenger in the given cabin, rounded to cents.</summary>
    public decimal PriceFor(CabinClass cabin) =>
        Math.Round(BasePrice * CabinMultipliers[cabin], 2, MidpointRounding.AwayFromZero);

    /// <summary>Total the API charges for this booking. Never trust a client-supplied figure.</summary>
    public decimal TotalFor(CabinClass cabin, int passengerCount)
    {
        if (passengerCount < 1)
            throw new DomainException("A booking needs at least one passenger.");

        return PriceFor(cabin) * passengerCount;
    }

    /// <summary>
    /// Takes seats out of inventory. Guarded by the xmin concurrency token in
    /// FlightConfiguration, so two simultaneous bookings for the last seat make
    /// one of them fail with a DbUpdateConcurrencyException rather than oversell.
    /// </summary>
    public void ReserveSeats(int count)
    {
        if (count < 1)
            throw new DomainException("Seat count must be at least one.");
        if (count > AvailableSeats)
            throw new DomainException($"Only {AvailableSeats} seat(s) left on flight {FlightNumber}.");

        AvailableSeats -= count;
    }

    /// <summary>Returns seats to inventory when a booking is cancelled or payment fails.</summary>
    public void ReleaseSeats(int count)
    {
        if (count < 1) return;
        AvailableSeats = Math.Min(AvailableSeats + count, TotalSeats);
    }

    public bool HasSeatsFor(int passengerCount) => AvailableSeats >= passengerCount;
}
