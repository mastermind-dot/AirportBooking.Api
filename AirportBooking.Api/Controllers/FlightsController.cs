using AirportBooking.Api.Extensions;
using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Flights;
using AirportBooking.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AirportBooking.Api.Controllers;

/// <summary>
/// Public reads. Browsing flights and prices does not require an account —
/// authentication starts at the booking step, where a booking has to belong to
/// somebody.
/// </summary>
[ApiController]
[Route("api/flights")]
[EnableRateLimiting(ApiPolicies.SearchRateLimit)]
public sealed class FlightsController : ControllerBase
{
    private readonly IFlightService _flightService;

    public FlightsController(IFlightService flightService)
    {
        _flightService = flightService;
    }

    /// <summary>
    /// Searches flights for one route on one date.
    /// GET /api/flights/search?origin=BRU&amp;destination=LHR&amp;departureDate=2026-09-15
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<FlightSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Search(
        [FromQuery] FlightSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _flightService.SearchAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            // An unknown airport code or a same-airport route is a bad request,
            // not an empty result: the caller asked something that cannot be
            // answered, rather than something with no matches.
            return Problem(
                title: "Search could not be performed",
                detail: result.Error!.Message,
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FlightDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _flightService.GetByIdAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                title: "Flight not found",
                detail: result.Error!.Message,
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
        }

        return Ok(result.Value);
    }
}

/// <summary>Reference data for the origin and destination pickers.</summary>
[ApiController]
[Route("api/airports")]
[EnableRateLimiting(ApiPolicies.SearchRateLimit)]
public sealed class AirportsController : ControllerBase
{
    private readonly IFlightService _flightService;

    public AirportsController(IFlightService flightService)
    {
        _flightService = flightService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AirportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var airports = await _flightService.GetAirportsAsync(cancellationToken);

        // Reference data that changes about never. Letting the browser and any
        // CDN hold it keeps the picker off the database on every page load.
        Response.Headers.CacheControl = "public, max-age=3600";

        return Ok(airports);
    }
}
