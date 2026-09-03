using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace AirportBooking.Api.Extensions;

public static class LocalizationExtensions
{
    /// <summary>
    /// French first: Malu Aviation operates in the DRC and most visitors read
    /// French. English is supported, not assumed.
    /// </summary>
    private static readonly string[] SupportedCultures = ["fr", "en"];

    public static IServiceCollection AddRequestLocalization(this IServiceCollection services)
    {
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = SupportedCultures.Select(c => new CultureInfo(c)).ToList();

            options.DefaultRequestCulture = new RequestCulture("fr");
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;

            // The SPA sends Accept-Language on every call, so the header is the
            // only provider that makes sense here. Cookie and query-string
            // providers are removed: this API has no pages of its own to switch
            // language on, and leaving them enabled would let a crafted URL
            // change the culture of a request.
            options.RequestCultureProviders =
                [new AcceptLanguageHeaderRequestCultureProvider()];

            // "fr-CD" and "fr-BE" should both resolve to "fr" rather than
            // falling through to the default.
            options.ApplyCurrentCultureToResponseHeaders = true;
        });

        return services;
    }

    /// <summary>
    /// Fails startup outside Development if no frontend origin is configured.
    ///
    /// An empty allow-list is not a safe default in disguise — it silently
    /// blocks the real site, which looks like a broken deployment rather than a
    /// missing setting. Better to refuse to start and say which key is absent.
    /// </summary>
    public static void ValidateCorsConfiguration(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            return;
        }

        var origins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (origins.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must list the frontend origin(s) outside Development.");
        }

        foreach (var origin in origins)
        {
            if (origin.Contains("localhost", StringComparison.OrdinalIgnoreCase))
            {
                app.Logger.LogWarning(
                    "CORS allows {Origin} outside Development. Remove it before this is public.",
                    origin);
            }

            if (origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                app.Logger.LogWarning(
                    "CORS allows the insecure origin {Origin}. The refresh cookie is Secure and will not be sent to it.",
                    origin);
            }
        }
    }
}
