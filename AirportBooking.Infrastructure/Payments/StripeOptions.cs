namespace AirportBooking.Infrastructure.Payments;

/// <summary>
/// Bound from the "Stripe" configuration section. Both values are secrets and
/// belong in user-secrets locally, and in environment variables or a vault in
/// production — never in appsettings.json.
/// </summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>The "sk_test_…" or "sk_live_…" key. Never leaves the server.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// The "whsec_…" signing secret. Without it a webhook cannot be verified,
    /// and an unverified webhook is just an anonymous request claiming a booking
    /// was paid for.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Deliberately not required at startup. The rest of the API — search,
    /// booking, auth — works fine without Stripe configured, and refusing to
    /// boot would block all of it. The payment endpoints report 503 instead.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(WebhookSecret);
}
