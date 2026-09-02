namespace AirportBooking.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant is violated (an illegal status transition,
/// overbooking a flight). The API's exception middleware maps this to a
/// 409 Conflict with a problem+json body, so these messages are user-facing:
/// keep them free of internal detail.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
