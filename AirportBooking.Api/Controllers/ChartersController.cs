using System.Security.Claims;
using AirportBooking.Api.Extensions;
using AirportBooking.Application.DTOs.Charters;
using AirportBooking.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AirportBooking.Api.Controllers;

/// <summary>
/// Charter quote enquiries — the other half of the business. Scheduled seats on
/// the Goma SD360 runs are booked through /api/bookings; everything else is
/// priced by a person, and this is how the enquiry reaches them.
/// </summary>
[ApiController]
[Route("api/charters")]
public sealed class ChartersController : ControllerBase
{
    private readonly ICharterService _charterService;

    public ChartersController(ICharterService charterService)
    {
        _charterService = charterService;
    }

    /// <summary>
    /// Submits an enquiry. Anonymous on purpose: requiring an account before
    /// someone can ask for a price would lose most of the enquiries worth
    /// having. Rate limiting carries the load that authentication would.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(ApiPolicies.CharterRateLimit)]
    [ProducesResponseType(typeof(CharterRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(CreateCharterRequest request, CancellationToken cancellationToken)
    {
        // Present when signed in, absent otherwise — both are fine.
        Guid? userId = Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : null;

        var locale = Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0].Split('-')[0]
                     ?? "fr";

        var result = await _charterService.SubmitAsync(request, userId, locale, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Enquiry could not be submitted",
                detail: result.Error!.Message);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>A signed-in customer's own enquiries, so they can chase one up.</summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<CharterRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _charterService.GetForUserAsync(userId, cancellationToken));
    }
}
