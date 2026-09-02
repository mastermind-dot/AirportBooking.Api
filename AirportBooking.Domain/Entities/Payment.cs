using AirportBooking.Domain.Enums;
using AirportBooking.Domain.Exceptions;

namespace AirportBooking.Domain.Entities;

/// <summary>
/// The record of a Stripe charge against a booking.
///
/// Deliberately absent: card number, expiry, CVC, cardholder name — anything
/// that would put this database in PCI scope. The card is entered in Stripe's
/// hosted iframe and never reaches the API. All that is kept here are Stripe's
/// own identifiers, which are useless to an attacker without your secret key.
/// </summary>
public class Payment
{
    private Payment() { } // EF Core

    public Payment(Guid bookingId, string stripePaymentIntentId, decimal amount, string currency)
    {
        Id = Guid.NewGuid();
        BookingId = bookingId;
        StripePaymentIntentId = stripePaymentIntentId;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid BookingId { get; private set; }
    public Booking Booking { get; private set; } = null!;

    /// <summary>Stripe's "pi_..." id. Unique — it is the idempotency key for webhook handling.</summary>
    public string StripePaymentIntentId { get; private set; } = null!;

    /// <summary>Stripe's "ch_..." id, known only once the charge succeeds.</summary>
    public string? StripeChargeId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "EUR";

    public PaymentStatus Status { get; private set; }

    /// <summary>Stripe's decline reason, safe to store and show ("card_declined").</summary>
    public string? FailureReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Called from the webhook handler on payment_intent.succeeded.</summary>
    public void MarkSucceeded(string? stripeChargeId)
    {
        if (Status == PaymentStatus.Succeeded) return; // webhooks retry; stay idempotent
        if (Status == PaymentStatus.Refunded)
            throw new DomainException("A refunded payment cannot succeed again.");

        Status = PaymentStatus.Succeeded;
        StripeChargeId = stripeChargeId;
        FailureReason = null;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Called from the webhook handler on payment_intent.payment_failed.</summary>
    public void MarkFailed(string? reason)
    {
        if (Status == PaymentStatus.Succeeded)
            throw new DomainException("A succeeded payment cannot be marked failed.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkRefunded()
    {
        if (Status != PaymentStatus.Succeeded)
            throw new DomainException("Only a succeeded payment can be refunded.");

        Status = PaymentStatus.Refunded;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
