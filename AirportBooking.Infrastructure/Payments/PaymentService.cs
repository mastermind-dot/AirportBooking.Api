using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Payments;
using AirportBooking.Application.Interfaces;
using AirportBooking.Domain.Entities;
using AirportBooking.Domain.Enums;
using AirportBooking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AirportBooking.Infrastructure.Payments;

public sealed class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(AppDbContext db, IPaymentGateway gateway, ILogger<PaymentService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<Result<PaymentIntentResponse>> CreateIntentAsync(
        Guid userId,
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_gateway.IsConfigured)
        {
            return Result<PaymentIntentResponse>.Failure(Error.PaymentsUnavailable);
        }

        // Ownership is in the predicate, so another user's booking is never
        // loaded — same rule as the bookings endpoints.
        var booking = await _db.Bookings
            .Include(b => b.Payment)
            .SingleOrDefaultAsync(b => b.Id == request.BookingId && b.UserId == userId, cancellationToken);

        if (booking is null)
        {
            return Result<PaymentIntentResponse>.Failure(Error.BookingNotFound);
        }

        if (booking.Status != BookingStatus.Pending)
        {
            // Already confirmed, cancelled or failed. Paying again would take
            // money for something that is not on sale.
            return Result<PaymentIntentResponse>.Failure(Error.BookingNotPayable);
        }

        // Resuming a checkout — a refreshed page, a returning user — reuses the
        // existing intent rather than opening a second one against the same
        // booking. Stripe is asked for it because the client secret is not
        // stored here and should not be.
        if (booking.Payment is { Status: PaymentStatus.Pending } existing)
        {
            var reuse = await _gateway.GetIntentAsync(existing.StripePaymentIntentId, cancellationToken);

            if (reuse.IsSuccess)
            {
                return Result<PaymentIntentResponse>.Success(new PaymentIntentResponse(
                    reuse.Value!.ClientSecret,
                    booking.Reference,
                    booking.TotalAmount,
                    booking.Currency));
            }

            // The stored intent is unusable — expired, cancelled, or already
            // settled. Fall through and create a replacement.
            _logger.LogInformation(
                "Existing intent {IntentId} for booking {Reference} could not be reused; creating a new one.",
                existing.StripePaymentIntentId, booking.Reference);
        }

        var metadata = new Dictionary<string, string>
        {
            ["bookingId"] = booking.Id.ToString(),
            ["bookingReference"] = booking.Reference,
            ["userId"] = userId.ToString()
        };

        var created = await _gateway.CreateIntentAsync(
            // Straight from the booking. This figure was computed server-side
            // from the flight's fare when the booking was made, and no request
            // between then and now can have altered it.
            booking.TotalAmount,
            booking.Currency,

            // Keyed on the booking, so two simultaneous Pay clicks resolve to
            // one intent at Stripe rather than two charges.
            idempotencyKey: $"booking-intent-{booking.Id}",
            metadata,
            receiptEmail: booking.ContactEmail,
            description: $"SkyBook booking {booking.Reference}",
            cancellationToken);

        if (created.IsFailure)
        {
            return Result<PaymentIntentResponse>.Failure(created.Error!);
        }

        var intent = created.Value!;

        if (booking.Payment is null)
        {
            var payment = new Payment(booking.Id, intent.PaymentIntentId, booking.TotalAmount, booking.Currency);
            _db.Payments.Add(payment);
            booking.AttachPayment(payment);
        }
        else
        {
            // Replacing a dead intent. The row is rebuilt rather than mutated so
            // the unique index on the intent id stays truthful.
            _db.Payments.Remove(booking.Payment);
            await _db.SaveChangesAsync(cancellationToken);

            var payment = new Payment(booking.Id, intent.PaymentIntentId, booking.TotalAmount, booking.Currency);
            _db.Payments.Add(payment);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PaymentIntent {IntentId} created for booking {Reference} ({Amount} {Currency}).",
            intent.PaymentIntentId, booking.Reference, booking.TotalAmount, booking.Currency);

        return Result<PaymentIntentResponse>.Success(new PaymentIntentResponse(
            intent.ClientSecret,
            booking.Reference,
            booking.TotalAmount,
            booking.Currency));
    }

    public async Task<Result> HandleWebhookAsync(
        string payload,
        string? signatureHeader,
        CancellationToken cancellationToken = default)
    {
        var parsed = _gateway.ParseEvent(payload, signatureHeader);

        if (parsed.IsFailure)
        {
            return Result.Failure(parsed.Error!);
        }

        // A genuine Stripe event we do not act on. Acknowledged so Stripe stops
        // redelivering it.
        if (parsed.Value is not { } paymentEvent)
        {
            return Result.Success();
        }

        var payment = await _db.Payments
            .Include(p => p.Booking).ThenInclude(b => b.Flight)
            .Include(p => p.Booking).ThenInclude(b => b.Passengers)
            .SingleOrDefaultAsync(p => p.StripePaymentIntentId == paymentEvent.PaymentIntentId, cancellationToken);

        if (payment is null)
        {
            // Nothing to match: a test event, an intent from another
            // environment, or a booking since deleted. Reporting failure would
            // make Stripe retry this for days over something unmatchable.
            _logger.LogWarning(
                "Webhook for unknown PaymentIntent {IntentId}; acknowledged and ignored.",
                paymentEvent.PaymentIntentId);

            return Result.Success();
        }

        var booking = payment.Booking;

        switch (paymentEvent.Kind)
        {
            case GatewayEventKind.Succeeded:
                payment.MarkSucceeded(paymentEvent.ChargeId);

                // Guarded rather than called blindly: Stripe delivers at least
                // once, and MarkConfirmed on a cancelled booking would throw —
                // which would become a 500 and an endless retry loop.
                if (booking.Status == BookingStatus.Pending)
                {
                    booking.MarkConfirmed();

                    _logger.LogInformation(
                        "Booking {Reference} confirmed by webhook (charge {ChargeId}).",
                        booking.Reference, paymentEvent.ChargeId);
                }
                break;

            case GatewayEventKind.Failed:
                payment.MarkFailed(paymentEvent.FailureReason);

                // The Pending check is what makes seat release idempotent. A
                // redelivered failure must not hand the same seats back twice.
                if (booking.Status == BookingStatus.Pending)
                {
                    booking.MarkFailed();
                    booking.Flight.ReleaseSeats(booking.PassengerCount);

                    _logger.LogInformation(
                        "Booking {Reference} failed ({Reason}); {Seats} seat(s) released.",
                        booking.Reference, paymentEvent.FailureReason, booking.PassengerCount);
                }
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
