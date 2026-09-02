namespace AirportBooking.Domain.Enums;

/// <summary>
/// Lifecycle of a booking. A booking is only ever moved to
/// <see cref="Confirmed"/> by a verified Stripe webhook, never by the browser.
/// </summary>
public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Failed = 3
}
