using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Flights;
using AirportBooking.Application.Interfaces;
using AirportBooking.Domain.Entities;
using AirportBooking.Domain.Enums;
using AirportBooking.Infrastructure.Common;
using AirportBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AirportBooking.Infrastructure.Flights;

public sealed class FlightService : IFlightService
{
    private readonly AppDbContext _db;
    private readonly ILogger<FlightService> _logger;

    public FlightService(AppDbContext db, ILogger<FlightService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<PagedResult<FlightSummaryDto>>> SearchAsync(
        FlightSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var originCode = request.Origin.Trim().ToUpperInvariant();
        var destinationCode = request.Destination.Trim().ToUpperInvariant();

        if (originCode == destinationCode)
        {
            return Result<PagedResult<FlightSummaryDto>>.Failure(Error.SameOriginAndDestination);
        }

        // Both airports in one round trip rather than two.
        var airports = await _db.Airports
            .AsNoTracking()
            .Where(a => a.IataCode == originCode || a.IataCode == destinationCode)
            .ToListAsync(cancellationToken);

        var origin = airports.SingleOrDefault(a => a.IataCode == originCode);
        if (origin is null)
        {
            return Result<PagedResult<FlightSummaryDto>>.Failure(Error.UnknownAirport(originCode));
        }

        var destination = airports.SingleOrDefault(a => a.IataCode == destinationCode);
        if (destination is null)
        {
            return Result<PagedResult<FlightSummaryDto>>.Failure(Error.UnknownAirport(destinationCode));
        }

        var originZone = AirportClock.Resolve(origin.TimeZoneId, _logger);
        var destinationZone = AirportClock.Resolve(destination.TimeZoneId, _logger);

        // The requested date is a calendar day at the origin, so its UTC bounds
        // depend on that airport's offset — in New Zealand the day starts before
        // it does in UTC, in Los Angeles well after.
        var dayStartUtc = AirportClock.ToUtc(request.DepartureDate.ToDateTime(TimeOnly.MinValue), originZone);
        var dayEndUtc = AirportClock.ToUtc(request.DepartureDate.AddDays(1).ToDateTime(TimeOnly.MinValue), originZone);

        var now = DateTime.UtcNow;

        var query = _db.Flights
            .AsNoTracking()
            .Where(f => f.OriginAirportId == origin.Id
                     && f.DestinationAirportId == destination.Id
                     && f.DepartureTimeUtc >= dayStartUtc
                     && f.DepartureTimeUtc < dayEndUtc
                     && f.DepartureTimeUtc > now
                     && f.AvailableSeats >= request.Passengers);

        query = ApplyFilters(query, request, originZone);
        query = ApplySort(query, request);

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return Result<PagedResult<FlightSummaryDto>>.Success(
                PagedResult<FlightSummaryDto>.Empty(request.Page, request.PageSize));
        }

        // Projected in the database, so Postgres returns the eight columns the
        // DTO needs rather than whole entities.
        var rows = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(f => new FlightRow(
                f.Id,
                f.FlightNumber,
                f.AirlineName,
                f.AirlineIataCode,
                f.AircraftType,
                f.DepartureTimeUtc,
                f.ArrivalTimeUtc,
                f.BasePrice,
                f.Currency,
                f.Stops,
                f.AvailableSeats))
            .ToListAsync(cancellationToken);

        var originDto = ToDto(origin);
        var destinationDto = ToDto(destination);

        var items = rows
            .Select(row => ToSummary(row, originDto, destinationDto, originZone, destinationZone, request))
            .ToList();

        return Result<PagedResult<FlightSummaryDto>>.Success(
            new PagedResult<FlightSummaryDto>(items, request.Page, request.PageSize, totalCount));
    }

    public async Task<Result<FlightDetailsDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var flight = await _db.Flights
            .AsNoTracking()
            .Include(f => f.Origin)
            .Include(f => f.Destination)
            .SingleOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (flight is null)
        {
            return Result<FlightDetailsDto>.Failure(Error.FlightNotFound);
        }

        var originZone = AirportClock.Resolve(flight.Origin.TimeZoneId, _logger);
        var destinationZone = AirportClock.Resolve(flight.Destination.TimeZoneId, _logger);

        // The whole fare table, so choosing a cabin on the details page does not
        // need another request.
        var fares = Enum.GetValues<CabinClass>()
            .ToDictionary(cabin => cabin.ToString(), flight.PriceFor);

        return Result<FlightDetailsDto>.Success(new FlightDetailsDto(
            flight.Id,
            flight.FlightNumber,
            flight.AirlineName,
            flight.AircraftType,
            flight.AirlineIataCode,
            ToDto(flight.Origin),
            ToDto(flight.Destination),
            flight.DepartureTimeUtc,
            AirportClock.ToLocal(flight.DepartureTimeUtc, originZone),
            flight.ArrivalTimeUtc,
            AirportClock.ToLocal(flight.ArrivalTimeUtc, destinationZone),
            (int)flight.Duration.TotalMinutes,
            flight.Stops,
            flight.IsDirect,
            flight.TotalSeats,
            flight.AvailableSeats,
            flight.Currency,
            fares));
    }

    public async Task<IReadOnlyList<AirportDto>> GetAirportsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Airports
            .AsNoTracking()
            .OrderBy(a => a.City)
            .ThenBy(a => a.IataCode)
            .Select(a => new AirportDto(a.IataCode, a.Name, a.City, a.CountryCode))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Flight> ApplyFilters(
        IQueryable<Flight> query,
        FlightSearchRequest request,
        TimeZoneInfo originZone)
    {
        if (request.MaxStops is { } maxStops)
        {
            query = query.Where(f => f.Stops <= maxStops);
        }

        if (request.Airlines is { Length: > 0 } airlines)
        {
            var codes = airlines.Select(a => a.Trim().ToUpperInvariant()).ToArray();
            query = query.Where(f => codes.Contains(f.AirlineIataCode));
        }

        // Price bounds are per passenger in the chosen cabin. The multiplier is a
        // constant here, so this stays a SQL comparison against BasePrice rather
        // than pulling rows into memory to call PriceFor on each one. Rounding to
        // cents is skipped, which can only matter within one cent of a boundary.
        var multiplier = Flight.MultiplierFor(request.Cabin);

        if (request.MinPrice is { } minPrice)
        {
            query = query.Where(f => f.BasePrice * multiplier >= minPrice);
        }

        if (request.MaxPrice is { } maxPrice)
        {
            query = query.Where(f => f.BasePrice * multiplier <= maxPrice);
        }

        // A time-of-day filter is local to the origin. Every flight in this query
        // departs from the same airport on the same day, so the window converts
        // to a pair of UTC instants once instead of per row.
        if (request.DepartAfter is { } after)
        {
            var afterUtc = AirportClock.ToUtc(request.DepartureDate.ToDateTime(after), originZone);
            query = query.Where(f => f.DepartureTimeUtc >= afterUtc);
        }

        if (request.DepartBefore is { } before)
        {
            var beforeUtc = AirportClock.ToUtc(request.DepartureDate.ToDateTime(before), originZone);
            query = query.Where(f => f.DepartureTimeUtc <= beforeUtc);
        }

        return query;
    }

    /// <summary>
    /// Every ordering ends with Id. Without a deterministic tiebreaker, rows that
    /// compare equal can land in a different order between the page-1 and page-2
    /// queries, so a flight is shown twice or skipped entirely.
    /// </summary>
    private static IQueryable<Flight> ApplySort(IQueryable<Flight> query, FlightSearchRequest request)
    {
        // Sorting on BasePrice is equivalent to sorting on the cabin fare,
        // because every cabin multiplier is positive.
        return (request.SortBy, request.Descending) switch
        {
            (FlightSortField.Price, false) =>
                query.OrderBy(f => f.BasePrice).ThenBy(f => f.DepartureTimeUtc).ThenBy(f => f.Id),
            (FlightSortField.Price, true) =>
                query.OrderByDescending(f => f.BasePrice).ThenBy(f => f.DepartureTimeUtc).ThenBy(f => f.Id),

            (FlightSortField.Duration, false) =>
                query.OrderBy(f => f.ArrivalTimeUtc - f.DepartureTimeUtc).ThenBy(f => f.Id),
            (FlightSortField.Duration, true) =>
                query.OrderByDescending(f => f.ArrivalTimeUtc - f.DepartureTimeUtc).ThenBy(f => f.Id),

            (_, true) =>
                query.OrderByDescending(f => f.DepartureTimeUtc).ThenBy(f => f.Id),
            _ =>
                query.OrderBy(f => f.DepartureTimeUtc).ThenBy(f => f.Id)
        };
    }

    private static FlightSummaryDto ToSummary(
        FlightRow row,
        AirportDto origin,
        AirportDto destination,
        TimeZoneInfo originZone,
        TimeZoneInfo destinationZone,
        FlightSearchRequest request)
    {
        var perPassenger = Math.Round(
            row.BasePrice * Flight.MultiplierFor(request.Cabin),
            2,
            MidpointRounding.AwayFromZero);

        return new FlightSummaryDto(
            row.Id,
            row.FlightNumber,
            row.AirlineName,
            row.AircraftType,
            row.AirlineIataCode,
            origin,
            destination,
            row.DepartureTimeUtc,
            AirportClock.ToLocal(row.DepartureTimeUtc, originZone),
            row.ArrivalTimeUtc,
            AirportClock.ToLocal(row.ArrivalTimeUtc, destinationZone),
            (int)(row.ArrivalTimeUtc - row.DepartureTimeUtc).TotalMinutes,
            row.Stops,
            row.Stops == 0,
            request.Cabin,
            perPassenger,
            perPassenger * request.Passengers,
            row.Currency,
            row.AvailableSeats);
    }

    private static AirportDto ToDto(Airport airport) =>
        new(airport.IataCode, airport.Name, airport.City, airport.CountryCode);

    /// <summary>The columns the list view needs, and nothing else.</summary>
    private sealed record FlightRow(
        Guid Id,
        string FlightNumber,
        string AirlineName,
        string AirlineIataCode,
        string AircraftType,
        DateTime DepartureTimeUtc,
        DateTime ArrivalTimeUtc,
        decimal BasePrice,
        string Currency,
        int Stops,
        int AvailableSeats);
}
