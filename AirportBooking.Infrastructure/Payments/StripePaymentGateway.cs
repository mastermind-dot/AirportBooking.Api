using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Payments;
using AirportBooking.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace AirportBooking.Infrastructure.Payments;

/// <summary>
/// The only file in the solution that references Stripe.net.
///
/// Everything above this speaks in <see cref="GatewayIntent"/> and
/// <see cref="GatewayPaymentEvent"/>, so the booking rules never depend on a
/// particular provider — and a test can substitute a fake without a network.
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    /// <summary>
    /// Currencies with no minor unit: an amount of 1000 means 1000 yen, not
    /// 10.00. Multiplying these by 100 would overcharge by a hundred times.
    /// </summary>
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW",
        "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentGateway> _logger;
    private readonly PaymentIntentService? _intents;

    public StripePaymentGateway(IOptions<StripeOptions> options, ILogger<StripePaymentGateway> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.IsConfigured)
        {
            // An explicit client rather than the static StripeConfiguration.ApiKey,
            // so the key is scoped to this instance instead of process-global state.
            _intents = new PaymentIntentService(new StripeClient(_options.SecretKey));
        }
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<Result<GatewayIntent>> CreateIntentAsync(
        decimal amount,
        string currency,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        string? receiptEmail,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (_intents is null)
        {
            return Result<GatewayIntent>.Failure(Error.PaymentsUnavailable);
        }

        var options = new PaymentIntentCreateOptions
        {
            Amount = ToMinorUnits(amount, currency),
            Currency = currency.ToLowerInvariant(),
            Metadata = new Dictionary<string, string>(metadata),
            ReceiptEmail = receiptEmail,
            Description = description,

            // Stripe still offers whatever methods are enabled on the account,
            // but only those that complete on the page.
            //
            // AllowRedirects "never" is what removes the return_url requirement.
            // With "always", Stripe mandates a return_url at confirmation, and
            // the intent then depends on every caller remembering to supply one
            // — a coupling that fails at the moment of payment and only for the
            // caller that forgot. Redirect methods (Link, Amazon Pay) are worth
            // little to customers paying for a Goma-Bukavu seat, so the trade is
            // a narrower method list for a constraint that cannot be broken.
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never"
            }
        };

        try
        {
            var intent = await _intents.CreateAsync(
                options,
                new RequestOptions { IdempotencyKey = idempotencyKey },
                cancellationToken);

            return Result<GatewayIntent>.Success(new GatewayIntent(intent.Id, intent.ClientSecret));
        }
        catch (StripeException ex)
        {
            // Stripe's own message is safe to surface: it describes the request,
            // not our internals.
            _logger.LogError(ex, "Stripe rejected a PaymentIntent creation: {Message}", ex.Message);
            return Result<GatewayIntent>.Failure(Error.PaymentGateway(ex.StripeError?.Message ?? ex.Message));
        }
    }

    public async Task<Result<GatewayIntent>> GetIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (_intents is null)
        {
            return Result<GatewayIntent>.Failure(Error.PaymentsUnavailable);
        }

        try
        {
            var intent = await _intents.GetAsync(paymentIntentId, cancellationToken: cancellationToken);

            // An intent that is already finished cannot be paid again; the caller
            // should create a fresh one rather than hand back a dead secret.
            if (intent.Status is "succeeded" or "canceled")
            {
                return Result<GatewayIntent>.Failure(Error.BookingNotPayable);
            }

            return Result<GatewayIntent>.Success(new GatewayIntent(intent.Id, intent.ClientSecret));
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Could not retrieve PaymentIntent {IntentId}.", paymentIntentId);
            return Result<GatewayIntent>.Failure(Error.PaymentGateway(ex.StripeError?.Message ?? ex.Message));
        }
    }

    public Result<GatewayPaymentEvent?> ParseEvent(string payload, string? signatureHeader)
    {
        if (!_options.IsConfigured)
        {
            return Result<GatewayPaymentEvent?>.Failure(Error.PaymentsUnavailable);
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Result<GatewayPaymentEvent?>.Failure(Error.InvalidWebhookSignature);
        }

        Event stripeEvent;
        try
        {
            // Verifies the HMAC over the raw body and rejects timestamps outside
            // the tolerance window, which is what stops a captured webhook being
            // replayed later. throwOnApiVersionMismatch is off so a Stripe API
            // version bump does not take payments down.
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _options.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            // Log at warning, not error: on a public endpoint this is as likely
            // to be internet noise as a real problem.
            _logger.LogWarning("Webhook signature verification failed: {Message}", ex.Message);
            return Result<GatewayPaymentEvent?>.Failure(Error.InvalidWebhookSignature);
        }

        if (stripeEvent.Data.Object is not PaymentIntent intent)
        {
            return Result<GatewayPaymentEvent?>.Success(null);
        }

        return stripeEvent.Type switch
        {
            EventTypes.PaymentIntentSucceeded => Result<GatewayPaymentEvent?>.Success(
                new GatewayPaymentEvent(
                    GatewayEventKind.Succeeded,
                    intent.Id,
                    intent.LatestChargeId,
                    null)),

            EventTypes.PaymentIntentPaymentFailed => Result<GatewayPaymentEvent?>.Success(
                new GatewayPaymentEvent(
                    GatewayEventKind.Failed,
                    intent.Id,
                    intent.LatestChargeId,
                    intent.LastPaymentError?.Message)),

            // A real event we simply do not act on. Acknowledged, not rejected —
            // a non-2xx would make Stripe retry it for days.
            _ => Result<GatewayPaymentEvent?>.Success(null)
        };
    }

    /// <summary>
    /// Stripe takes amounts as integers in the currency's smallest unit, so
    /// EUR 12.34 is 1234. Rounding away from zero matches how the total was
    /// computed, so the charge always equals the figure shown to the user.
    /// </summary>
    private static long ToMinorUnits(decimal amount, string currency)
    {
        if (ZeroDecimalCurrencies.Contains(currency))
        {
            return (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        }

        return (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
    }
}
