using System.Text.Json.Serialization;
using AirportBooking.Api.BackgroundServices;
using AirportBooking.Api.Extensions;
using AirportBooking.Api.Filters;
using AirportBooking.Api.Middleware;
using AirportBooking.Application.Validators;
using AirportBooking.Infrastructure;
using AirportBooking.Infrastructure.Data.Seed;
using AirportBooking.Infrastructure.Payments;
using FluentValidation;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    // Applied globally so a new endpoint cannot forget to validate its input.
    options.Filters.Add<ValidationFilter>();
})
.AddJsonOptions(options =>
{
    // Enums as names, not ordinals. Query strings already accept
    // "cabin=Business", so returning 2 would make the API inconsistent with
    // itself — and it would silently change meaning if the enum were ever
    // reordered, exactly the reason BookingStatus is stored as text.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

// PostgreSQL, EF Core, Identity and the auth services. The connection string and
// signing key come from user-secrets in development, and from environment
// variables or a vault in production.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddAuthRateLimiting();

builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// Releases seats held by bookings that were never paid for.
builder.Services.AddHostedService<PendingBookingExpiryWorker>();

var app = builder.Build();

// First in the pipeline, so it can catch anything thrown further down.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Stripe is optional at boot so that search, booking and auth still run without
// it — but silence would be worse than a missing key, so say so once at startup.
// In production it is a hard failure: a deployment that cannot take money is
// broken, and should not come up pretending otherwise.
if (!app.Services.GetRequiredService<IOptions<StripeOptions>>().Value.IsConfigured)
{
    if (app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning(
            "Stripe is not configured; payment endpoints will return 503. Set it with: " +
            "dotnet user-secrets set \"Stripe:SecretKey\" \"sk_test_...\" and \"Stripe:WebhookSecret\" \"whsec_...\"");
    }
    else
    {
        throw new InvalidOperationException(
            "Stripe:SecretKey and Stripe:WebhookSecret must be configured outside Development.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Applies pending migrations and tops up the rolling flight schedule.
    // Development only: in production, migrations run as a deployment step so
    // two instances starting at once cannot race over the same schema.
    await app.Services.MigrateAndSeedAsync();
}
else
{
    // Tells browsers to refuse plain HTTP to this host in future. Left off in
    // development because the header is cached and would poison localhost for
    // any other project served over HTTP.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Order below this line is not stylistic — each of these depends on the one
// before it having run:
app.UseRouting();

//   CORS before auth, so the browser's preflight OPTIONS gets its headers
//   without needing a token it never sends on a preflight.
app.UseCors(ApiPolicies.FrontendCors);

//   Rate limiting before authentication, so floods are rejected cheaply instead
//   of paying for signature validation on every abusive request.
app.UseRateLimiter();

//   Authentication identifies the caller; authorization then decides. Reversing
//   these makes [Authorize] reject everyone, because nobody is signed in yet.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
