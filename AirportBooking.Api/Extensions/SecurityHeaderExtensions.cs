using NetEscapades.AspNetCore.SecurityHeaders;

namespace AirportBooking.Api.Extensions;

public static class SecurityHeaderExtensions
{
    /// <summary>
    /// Response headers for a JSON API.
    ///
    /// The policy is deliberately stricter than a website's would be: this host
    /// returns JSON and nothing else, so it never needs to load a script, embed
    /// a frame, or submit a form. Everything is denied by default rather than
    /// allow-listed, because there is nothing legitimate to allow.
    ///
    /// The single-page app's own policy is separate and lives with whatever
    /// serves it — it needs Stripe's script and iframe, which this host must not.
    /// </summary>
    public static IApplicationBuilder UseApiSecurityHeaders(
        this IApplicationBuilder app,
        IHostEnvironment environment)
    {
        var policies = new HeaderPolicyCollection()
            .AddFrameOptionsDeny()
            .AddContentTypeOptionsNoSniff()

            // No referrer at all. A booking URL can carry a reference or an id,
            // and there is no third party that needs to know where a request
            // came from.
            .AddReferrerPolicyNoReferrer()

            .AddContentSecurityPolicy(builder =>
            {
                // Nothing may load, anywhere. If a browser ever renders a
                // response from this host as a document — an error page, a
                // sniffed content type — it can execute nothing.
                builder.AddDefaultSrc().None();
                builder.AddFrameAncestors().None();
                builder.AddBaseUri().None();
                builder.AddFormAction().None();
            })

            .AddPermissionsPolicy(builder =>
            {
                // An API has no use for hardware. Denying it costs nothing and
                // removes the capability from any document served from here.
                builder.AddCamera().None();
                builder.AddMicrophone().None();
                builder.AddGeolocation().None();
                builder.AddPayment().None();
            })

            // Kestrel advertises itself by default. Version numbers help an
            // attacker pick an exploit and help nobody else.
            .RemoveServerHeader();

        if (!environment.IsDevelopment())
        {
            // Two years, subdomains included. Only outside development: the
            // header is cached per host, so setting it on localhost would force
            // HTTPS for every other project you serve from localhost, for years.
            policies.AddStrictTransportSecurityMaxAgeIncludeSubDomains(
                maxAgeInSeconds: 60 * 60 * 24 * 730);
        }

        return app.UseSecurityHeaders(policies);
    }
}
