using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Payments;

namespace AirportBooking.Application.Interfaces;

/// <summary>
/// The payment provider, described in terms this application understands.
/// Stripe types stop at the implementation, so swapping provider — or writing a
/// fake for tests — does not reach into the booking logic.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>False when no API key is configured; the endpoints then report 503 rather than failing obscurely.</summary>
    bool IsConfigured { get; }

    /// <param name="idempotencyKey">
    /// Replaying a create with the same key returns the original intent instead
    /// of a second one, so a double-clicked Pay button cannot produce two
    /// charges for one booking.
    /// </param>
    Task<Result<GatewayIntent>> CreateIntentAsync(
        decimal amount,
        string currency,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        string? receiptEmail,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches an existing intent, so a resumed checkout reuses it rather than opening another.</summary>
    Task<Result<GatewayIntent>> GetIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the webhook signature and reduces the payload to an event this
    /// application acts on.
    ///
    /// Failure means the signature did not verify — the request did not come
    /// from Stripe. Success carrying a null value means a genuine event of a
    /// kind we do not handle, which should still be acknowledged.
    /// </summary>
    Result<GatewayPaymentEvent?> ParseEvent(string payload, string? signatureHeader);
}
