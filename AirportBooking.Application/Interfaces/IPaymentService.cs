using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Payments;

namespace AirportBooking.Application.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Creates (or resumes) the Stripe PaymentIntent for a booking the caller
    /// owns. The amount is taken from the booking, never from the request.
    /// </summary>
    Task<Result<PaymentIntentResponse>> CreateIntentAsync(
        Guid userId,
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a signed webhook. This is what actually confirms a booking — the
    /// browser's result after confirming the card is for the user's benefit
    /// only, and a user who closes the tab must still end up confirmed.
    /// </summary>
    Task<Result> HandleWebhookAsync(
        string payload,
        string? signatureHeader,
        CancellationToken cancellationToken = default);
}
