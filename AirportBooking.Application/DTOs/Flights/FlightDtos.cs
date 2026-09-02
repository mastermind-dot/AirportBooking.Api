using AirportBooking.Domain.Enums;

namespace AirportBooking.Application.DTOs.Flights;

public enum FlightSortField
{
    DepartureTime = 0,
    Price = 1,
    Duration = 2
}

/// <summary>
/// Search criteria, bound from the query string.
///
/// A class with settable properties rather than a positional record: MVC binds
/// simple properties from a query string without surprises, and the defaults
/// here are the contract for what an unfiltered search means.
/// </summary>
public sealed class FlightSearchRequest
{
    /// <summary>Origin IATA code, e.g. "BRU".</summary>
    public string Origin { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    /// <summary>The calendar date at the <em>origin</em> airport, not in UTC.</summary>
    public DateOnly DepartureDate { get; set; }

    public int Passengers { get; set; } = 1;

    public CabinClass Cabin { get; set; } = CabinClass.Economy;

    /// <summary>Per-passenger price bounds in the flight's currency, for the chosen cabin.</summary>
    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    /// <summary>Airline IATA codes to include. Empty means all.</summary>
    public string[]? Airlines { get; set; }

    /// <summary>0 for direct flights only. Null means any.</summary>
    public int? MaxStops { get; set; }

    /// <summary>Earliest local departure time at the origin airport.</summary>
    public TimeOnly? DepartAfter { get; set; }

    public TimeOnly? DepartBefore { get; set; }

    public FlightSortField SortBy { get; set; } = FlightSortField.DepartureTime;

    public bool Descending { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed record AirportDto(
    string IataCode,
    string Name,
    string City,
    string CountryCode);

/// <summary>
/// A flight as it appears in a results list.
///
/// Times are given twice on purpose. The UTC value is the unambiguous instant
/// for any client-side maths; the local value is what a traveller actually reads
/// on a boarding pass, and computing it needs the airport's time zone — which
/// the server has and the browser does not.
/// </summary>
public sealed record FlightSummaryDto(
    Guid Id,
    string FlightNumber,
    string AirlineName,
    string AirlineIataCode,
    AirportDto Origin,
    AirportDto Destination,
    DateTime DepartureTimeUtc,
    DateTime DepartureTimeLocal,
    DateTime ArrivalTimeUtc,
    DateTime ArrivalTimeLocal,
    int DurationMinutes,
    int Stops,
    bool IsDirect,
    CabinClass Cabin,
    decimal PricePerPassenger,
    decimal TotalPrice,
    string Currency,
    int AvailableSeats);

/// <summary>
/// The detail view. Adds capacity and the full fare table so the page can offer
/// a cabin choice without a second round trip.
/// </summary>
public sealed record FlightDetailsDto(
    Guid Id,
    string FlightNumber,
    string AirlineName,
    string AirlineIataCode,
    AirportDto Origin,
    AirportDto Destination,
    DateTime DepartureTimeUtc,
    DateTime DepartureTimeLocal,
    DateTime ArrivalTimeUtc,
    DateTime ArrivalTimeLocal,
    int DurationMinutes,
    int Stops,
    bool IsDirect,
    int TotalSeats,
    int AvailableSeats,
    string Currency,
    IReadOnlyDictionary<string, decimal> FaresByCabin);
