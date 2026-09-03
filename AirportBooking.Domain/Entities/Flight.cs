using AirportBooking.Domain.Exceptions;

namespace AirportBooking.Domain.Entities;

/// <summary>
/// A scheduled flight and the single source of truth for its price.
/// The API never accepts an amount from the client — it reads BasePrice here
/// and multiplies by the passenger count.
/// </summary>
public class Flight
{
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
        string currency = "USD",
        string aircraftType = "Short SD360")
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
        AircraftType = aircraftType;
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

    /// <summary>
    /// The fare for one passenger. There is one cabin: the SD360 is a 30-seat
    /// turboprop with a single class, so a fare is a number rather than a table.
    /// </summary>
    public decimal BasePrice { get; private set; }

    /// <summary>ISO 4217 code. Stripe wants the amount in minor units of this currency.</summary>
    public string Currency { get; private set; } = "USD";

    public int TotalSeats { get; private set; }

    public int AvailableSeats { get; private set; }

    /// <summary>0 for direct flights. Drives the "direct only" filter.</summary>
    public int Stops { get; private set; }

    /// <summary>
    /// The airframe, e.g. "Short SD360". With a single operator the airline
    /// filter says nothing, but which aircraft flies a route says a great deal:
    /// it is what tells a passenger whether the strip is paved.
    /// </summary>
    public string AircraftType { get; private set; } = null!;

    public TimeSpan Duration => ArrivalTimeUtc - DepartureTimeUtc;

    public bool IsDirect => Stops == 0;

    /// <summary>Total the API charges for this booking. Never trust a client-supplied figure.</summary>
    public decimal TotalFor(int passengerCount)
    {
        if (passengerCount < 1)
            throw new DomainException("A booking needs at least one passenger.");

        return BasePrice * passengerCount;
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
