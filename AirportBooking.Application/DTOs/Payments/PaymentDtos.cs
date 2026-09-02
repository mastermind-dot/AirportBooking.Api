namespace AirportBooking.Application.DTOs.Payments;

/// <summary>
/// Identifies which booking to pay for — and nothing else.
///
/// There is no amount field, deliberately. The figure charged is read from the
/// booking, which itself was computed from the flight's stored fare. A client
/// has no way to express a price at any point in this flow.
/// </summary>
public sealed record CreatePaymentIntentRequest(Guid BookingId);

/// <summary>
/// What the browser needs to mount Stripe's Payment Element.
///
/// The client secret authorises confirming this one payment and nothing more.
/// It is safe in the browser — which is the point of the whole arrangement:
/// card details go from the Payment Element straight to Stripe, and never
/// touch this API.
/// </summary>
public sealed record PaymentIntentResponse(
    string ClientSecret,
    string BookingReference,
    decimal Amount,
    string Currency);

/// <summary>A payment intent as the gateway describes it, free of Stripe types.</summary>
public sealed record GatewayIntent(string PaymentIntentId, string ClientSecret);

/// <summary>
/// A verified webhook event, reduced to what the domain acts on.
/// </summary>
public sealed record GatewayPaymentEvent(
    GatewayEventKind Kind,
    string PaymentIntentId,
    string? ChargeId,
    string? FailureReason);

public enum GatewayEventKind
{
    Succeeded,
    Failed
}
