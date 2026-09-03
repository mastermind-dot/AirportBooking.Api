using System.Text.RegularExpressions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AirportBooking.Api.Extensions;

public static class LoggingExtensions
{
    /// <summary>
    /// Structured logging with scrubbing for the values this API handles that
    /// must never reach a log file: passwords, tokens, and passport numbers.
    ///
    /// Nothing in the code deliberately logs them — but logs are written by
    /// exception handlers, request loggers and future code, none of which know
    /// what is sensitive. Scrubbing at the sink is the only place the guarantee
    /// actually holds.
    /// </summary>
    public static void AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With<SensitiveDataMask>()
                .WriteTo.Console();
        });
    }
}

/// <summary>
/// Redacts secrets from rendered log messages and their properties.
///
/// A blocklist is the wrong shape for this problem in general — it fails open,
/// and anything not listed leaks. It is used here because the alternative,
/// annotating every DTO, fails silently the moment someone adds a field. Treat
/// this as the last line rather than the only one: still do not log the values.
/// </summary>
public sealed class SensitiveDataMask : ILogEventEnricher
{
    private const string Redacted = "[redacted]";

    /// <summary>Property names whose values never belong in a log, whatever they contain.</summary>
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "newPassword", "currentPassword",
        "accessToken", "refreshToken", "token", "clientSecret",
        "authorization", "apiKey", "secretKey", "webhookSecret",
        "passportNumber", "cardNumber", "cvc"
    };

    /// <summary>
    /// Values that look like a secret even when the property name is innocent —
    /// a Stripe key pasted into a message, a bearer token in an exception.
    /// </summary>
    private static readonly Regex SecretShaped = new(
        @"\b(sk_(?:test|live)_[A-Za-z0-9]{8,}|whsec_[A-Za-z0-9]{8,}|Bearer\s+[A-Za-z0-9\-._~+/]{20,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            if (SensitiveProperties.Contains(name))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Redacted)));
                continue;
            }

            // A property that merely contains a secret-shaped string — a request
            // body, an exception message — is masked in place rather than dropped,
            // so the surrounding context survives for debugging.
            if (value is ScalarValue { Value: string text } && SecretShaped.IsMatch(text))
            {
                logEvent.AddOrUpdateProperty(
                    new LogEventProperty(name, new ScalarValue(SecretShaped.Replace(text, Redacted))));
            }
        }
    }
}
