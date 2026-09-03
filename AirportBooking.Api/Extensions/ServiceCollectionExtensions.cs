using System.Text;
using System.Threading.RateLimiting;
using AirportBooking.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AirportBooking.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or shorter than 32 characters. Set it with: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"<64+ random chars>\"");
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // Without this, the handler rewrites "sub" to the long
                // ClaimTypes.NameIdentifier URI and claim lookups stop matching
                // what the token actually contains.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

                    ValidateLifetime = true,

                    // The 5-minute default would keep a 15-minute token alive a
                    // third longer than intended.
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        // Lets the client tell "expired, go refresh" apart from
                        // "malformed, sign in again" without parsing the token.
                        if (context.Exception is SecurityTokenExpiredException)
                        {
                            context.Response.Headers.Append("x-token-expired", "true");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// An explicit allow-list of frontend origins. A wildcard cannot be combined
    /// with AllowCredentials — the browser refuses it — and credentials are
    /// required for the refresh cookie to travel at all.
    /// </summary>
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(ApiPolicies.FrontendCors, policy =>
            {
                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()

                      // The SPA reads this header to know whether a 401 means
                      // "refresh" or "sign in".
                      .WithExposedHeaders("x-token-expired");
            });
        });

        return services;
    }

    /// <summary>
    /// Per-IP limits on the endpoints worth attacking. Identity's account
    /// lockout stops guessing at one account; this stops one client spraying
    /// many accounts, which lockout alone never sees.
    /// </summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(ApiPolicies.AuthRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),

                        // No queue: a rejected auth attempt should fail now.
                        // Queueing them just delays the same answer and holds
                        // server resources while an attacker keeps knocking.
                        QueueLimit = 0
                    }));

            // Search is public and a user legitimately hits it repeatedly while
            // adjusting filters, so the limit is far looser than auth. It exists
            // to stop scraping of the whole schedule, not to police normal use.
            options.AddPolicy(ApiPolicies.SearchRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),

                        // A small queue absorbs a burst from someone dragging a
                        // price slider, instead of failing a request they will
                        // immediately retry anyway.
                        QueueLimit = 10,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            // Starting a payment is a deliberate act — a handful per minute is
            // already generous. Tight because each one costs a Stripe API call,
            // and because repeated attempts on one booking are a sign of trouble
            // rather than of normal use. The webhook is exempt entirely: Stripe
            // retries in bursts, and throttling those delays real confirmations.
            options.AddPolicy(ApiPolicies.PaymentRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 15,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // An anonymous endpoint that writes to the database is the obvious
            // spam target on this API. Five an hour per IP is generous for a
            // real enquirer and useless to a bot; the queue is zero so a flood
            // is refused immediately rather than parked in memory.
            options.AddPolicy(ApiPolicies.CharterRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";

                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "You have made too many attempts. Wait a minute and try again."
                }, token);
            };
        });

        return services;
    }
}
