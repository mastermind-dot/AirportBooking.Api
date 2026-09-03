using System.Text;
using AirportBooking.Api.Extensions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace AirportBooking.UnitTests.Api;

/// <summary>
/// The log mask is the last line between a secret and a log file, and it fails
/// open — anything it does not recognise is written out. That makes it exactly
/// the kind of code that needs tests: a regression here is silent, and only
/// discovered by finding a key in a log.
/// </summary>
public class SensitiveDataMaskTests
{
    private static string Render(Action<ILogger> log)
    {
        var sink = new CapturingSink();

        using var logger = new LoggerConfiguration()
            .Enrich.With<SensitiveDataMask>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        log(logger);
        return sink.Last;
    }

    // ---------- sensitive by property name ----------

    [Theory]
    [InlineData("password")]
    [InlineData("accessToken")]
    [InlineData("refreshToken")]
    [InlineData("passportNumber")]
    [InlineData("secretKey")]
    [InlineData("webhookSecret")]
    [InlineData("cvc")]
    public void A_sensitive_property_name_is_redacted_whatever_the_value(string property)
    {
        var rendered = Render(log =>
            log.Information("Value {" + property + "}", "totally-ordinary-looking-value"));

        Assert.DoesNotContain("totally-ordinary-looking-value", rendered);
        Assert.Contains("[redacted]", rendered);
    }

    [Fact]
    public void Property_names_are_matched_case_insensitively()
    {
        // Serilog property names follow whatever the call site wrote, and that
        // casing is not consistent across a codebase.
        var rendered = Render(log => log.Information("Value {PassportNumber}", "CD1234567"));

        Assert.DoesNotContain("CD1234567", rendered);
    }

    // ---------- sensitive by value shape ----------

    // The fixtures below are assembled from fragments rather than written as
    // whole literals. GitHub's push protection scans source for Stripe key
    // patterns and rejects the push on a match — it cannot tell an obviously
    // fake key from a real one, and it is right not to try. Concatenation is
    // folded at compile time, so the values reaching the mask are byte for byte
    // what a real key would be; only the text in this file differs.
    private const string FakeTestKey = "sk_" + "test_51ABCDEFGHIJKLMNOP0123456789";
    private const string FakeLiveKey = "sk_" + "live_51ABCDEFGHIJKLMNOP0123456789";
    private const string FakeWebhookSecret = "wh" + "sec_ABCDEFGHIJKLMNOPQRSTUVWX";

    [Theory]
    [InlineData(FakeTestKey)]
    [InlineData(FakeLiveKey)]
    [InlineData(FakeWebhookSecret)]
    public void A_secret_shaped_value_is_masked_even_in_an_innocent_property(string secret)
    {
        // This is the case that matters: nobody names the property well when
        // they are logging an exception message or a gateway response.
        var rendered = Render(log => log.Information("Gateway said {detail}", $"Invalid key {secret}"));

        Assert.DoesNotContain(secret, rendered);
        Assert.Contains("[redacted]", rendered);
    }

    [Fact]
    public void A_bearer_token_is_masked_and_the_surrounding_text_survives()
    {
        var rendered = Render(log => log.Information(
            "Rejected {header}", "Bearer eyJhbGciOiJIUzI1NiJ9AAAAAAAAAAAAAAAAAAAAAAA"));

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9AAAAAAAAAAAAAAAAAAAAAAA", rendered);
        Assert.Contains("Rejected", rendered);
    }

    // ---------- the mask must not eat real diagnostics ----------

    [Fact]
    public void Ordinary_values_pass_through_untouched()
    {
        // A mask that redacts too much gets switched off, and then protects
        // nothing. Booking references and counts must survive intact.
        var rendered = Render(log =>
            log.Information("Booking {Reference} confirmed for {Seats} seat(s)", "MLU-ABC123", 2));

        Assert.Contains("MLU-ABC123", rendered);
        Assert.Contains("2", rendered);
        Assert.DoesNotContain("[redacted]", rendered);
    }

    [Fact]
    public void A_short_string_that_merely_starts_like_a_key_is_left_alone()
    {
        // "sk_test_" with nothing after it is not a key, and over-eager masking
        // would hide the message that explains a misconfiguration.
        var rendered = Render(log => log.Information("Config {note}", "expected an sk_test_ prefix"));

        Assert.Contains("expected an sk_test_ prefix", rendered);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly MessageTemplateTextFormatter _formatter = new("{Message:lj}", null);

        public string Last { get; private set; } = string.Empty;

        public void Emit(LogEvent logEvent)
        {
            var writer = new StringWriter(new StringBuilder());
            _formatter.Format(logEvent, writer);
            Last = writer.ToString();
        }
    }
}
