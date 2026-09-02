using System.Security.Claims;
using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Bookings;
using AirportBooking.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AirportBooking.Api.Controllers;

/// <summary>
/// Everything here is scoped to the signed-in user. The user id comes from the
/// validated token, never from the route or body — otherwise changing a number
/// in the URL would be enough to read somebody else's itinerary.
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
public sealed class BookingsController : ControllerBase
{
    private const int MaxPageSize = 50;

    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _bookingService.CreateAsync(userId, request, cancellationToken);

        if (result.IsFailure)
        {
            return FromError(result.Error!);
        }

        var booking = result.Value!;

        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BookingSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        // Clamped rather than rejected: paging parameters on a list the user
        // already owns are a convenience, and a 400 here helps nobody.
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var result = await _bookingService.GetForUserAsync(userId, page, pageSize, cancellationToken);

        return result.IsFailure ? FromError(result.Error!) : Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _bookingService.GetByIdAsync(userId, id, cancellationToken);

        return result.IsFailure ? FromError(result.Error!) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _bookingService.CancelAsync(userId, id, cancellationToken);

        return result.IsFailure ? FromError(result.Error!) : Ok(result.Value);
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    private IActionResult FromError(Error error)
    {
        var status = error.Code switch
        {
            "bookings.not_found" => StatusCodes.Status404NotFound,
            "flights.not_found" => StatusCodes.Status404NotFound,

            // The request was valid but the world moved: the seats went, or the
            // flight closed. 409, not 400 — resending it unchanged could succeed.
            "bookings.not_enough_seats" => StatusCodes.Status409Conflict,
            "bookings.seats_unavailable" => StatusCodes.Status409Conflict,
            "bookings.flight_departed" => StatusCodes.Status409Conflict,
            "bookings.not_cancellable" => StatusCodes.Status409Conflict,

            _ => StatusCodes.Status400BadRequest
        };

        var problem = Problem(
            statusCode: status,
            title: status == StatusCodes.Status404NotFound ? "Not found" : "Booking could not be completed",
            detail: error.Message);

        if (problem is ObjectResult { Value: ProblemDetails details })
        {
            details.Extensions["code"] = error.Code;
        }

        return problem;
    }
}
