using System.Security.Claims;
using AirportBooking.Api.Extensions;
using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Payments;
using AirportBooking.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AirportBooking.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the client secret for a booking the caller owns.
    ///
    /// The response carries no card fields and expects none: the browser sends
    /// card details straight to Stripe using this secret, which is what keeps
    /// this server out of PCI scope entirely.
    /// </summary>
    [HttpPost("create-intent")]
    [Authorize]
    [EnableRateLimiting(ApiPolicies.PaymentRateLimit)]
    [ProducesResponseType(typeof(PaymentIntentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateIntent(
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Unauthorized();
        }

        var result = await _paymentService.CreateIntentAsync(userId, request, cancellationToken);

        if (result.IsFailure)
        {
            var error = result.Error!;

            var status = error.Code switch
            {
                "bookings.not_found" => StatusCodes.Status404NotFound,
                "payments.booking_not_payable" => StatusCodes.Status409Conflict,
                "payments.unavailable" => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status502BadGateway
            };

            var problem = Problem(statusCode: status, title: "Payment could not be started", detail: error.Message);

            if (problem is ObjectResult { Value: ProblemDetails details })
            {
                details.Extensions["code"] = error.Code;
            }

            return problem;
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Stripe's callback, and the only thing that confirms a booking.
    ///
    /// Three deliberate exemptions from the rest of the pipeline:
    ///   AllowAnonymous  — Stripe has no account here and sends no token; the
    ///                     signature is the authentication.
    ///   DisableRateLimiting — Stripe retries a failed delivery in bursts, and
    ///                     throttling those would delay confirming real bookings.
    ///   raw body read   — the signature covers the exact bytes sent, so the
    ///                     payload must not pass through model binding first.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [DisableRateLimiting]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        // No body parameter on this action, so nothing has consumed the stream.
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        var result = await _paymentService.HandleWebhookAsync(payload, signature, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Rejected webhook: {Code}", result.Error!.Code);

            // 400 tells Stripe not to bother retrying — a signature that does not
            // verify will never verify. Anything we simply could not match is
            // reported as success by the service for the opposite reason.
            return BadRequest(new { error = result.Error.Code });
        }

        return Ok();
    }
}
