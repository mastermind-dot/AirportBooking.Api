using AirportBooking.Domain.Entities;
using AirportBooking.Domain.Enums;
using AirportBooking.Domain.Exceptions;

namespace AirportBooking.UnitTests.Domain;

/// <summary>
/// Payment mirrors what Stripe reports. Every transition here is reached from a
/// webhook, so the same event arriving twice must leave the same state — and a
/// contradictory event must not be able to rewrite history.
/// </summary>
public class PaymentTests
{
    private static Payment Pending() =>
        new(bookingId: Guid.NewGuid(), stripePaymentIntentId: "pi_test_123", amount: 308m, currency: "USD");

    [Fact]
    public void A_new_payment_is_pending_and_has_no_charge()
    {
        var payment = Pending();

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.StripeChargeId);
        Assert.Null(payment.CompletedAtUtc);
    }

    [Fact]
    public void MarkSucceeded_records_the_charge()
    {
        var payment = Pending();

        payment.MarkSucceeded("ch_test_456");

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("ch_test_456", payment.StripeChargeId);
        Assert.NotNull(payment.CompletedAtUtc);
    }

    [Fact]
    public void MarkSucceeded_is_idempotent()
    {
        var payment = Pending();
        payment.MarkSucceeded("ch_test_456");
        var firstCompletedAt = payment.CompletedAtUtc;

        payment.MarkSucceeded("ch_test_456");

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(firstCompletedAt, payment.CompletedAtUtc);
    }

    [Fact]
    public void A_retry_that_succeeds_clears_the_earlier_failure_reason()
    {
        // Stripe allows a declined intent to be retried on another card. The
        // stale decline message must not survive onto a payment that worked.
        var payment = Pending();
        payment.MarkFailed("card_declined");

        payment.MarkSucceeded("ch_test_456");

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Null(payment.FailureReason);
    }

    [Fact]
    public void MarkFailed_records_the_reason()
    {
        var payment = Pending();

        payment.MarkFailed("insufficient_funds");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("insufficient_funds", payment.FailureReason);
    }

    [Fact]
    public void A_succeeded_payment_cannot_be_marked_failed()
    {
        // Out-of-order delivery is normal. A late payment_failed must not
        // reverse a success that already released the booking.
        var payment = Pending();
        payment.MarkSucceeded("ch_test_456");

        Assert.Throws<DomainException>(() => payment.MarkFailed("card_declined"));
    }

    [Fact]
    public void Only_a_succeeded_payment_can_be_refunded()
    {
        var payment = Pending();

        Assert.Throws<DomainException>(payment.MarkRefunded);
    }

    [Fact]
    public void A_refunded_payment_cannot_succeed_again()
    {
        var payment = Pending();
        payment.MarkSucceeded("ch_test_456");
        payment.MarkRefunded();

        Assert.Throws<DomainException>(() => payment.MarkSucceeded("ch_test_456"));
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
    }

    [Fact]
    public void The_schema_holds_no_card_data()
    {
        // The guarantee is structural, not a policy someone has to remember:
        // there is no property a card number could be written to.
        var properties = typeof(Payment).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();

        Assert.DoesNotContain(properties, p =>
            p.Contains("card") || p.Contains("pan") || p.Contains("cvc") ||
            p.Contains("expiry") || p.Contains("cardholder"));
    }
}
