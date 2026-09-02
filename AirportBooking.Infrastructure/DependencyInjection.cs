using AirportBooking.Application.Interfaces;
using AirportBooking.Domain.Entities;
using AirportBooking.Infrastructure.Data;
using AirportBooking.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AirportBooking.Infrastructure;

/// <summary>
/// Infrastructure registers its own services, so Program.cs never has to know
/// that the store happens to be Postgres or that auth happens to use Identity.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. Set it with: " +
                "dotnet user-secrets set \"ConnectionStrings:Default\" \"Host=localhost;...\"");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Migrations live in this assembly, not in the startup project.
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                // Transient network faults get retried instead of surfacing as
                // a 500. Note this is incompatible with user-initiated
                // transactions — use the execution strategy explicitly there.
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            });
        });

        services.AddIdentityServices();
        services.AddJwtOptions(configuration);

        return services;
    }

    private static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        // AddIdentityCore, not AddIdentity: the full version registers cookie
        // authentication schemes and sets itself as the default, which would
        // silently take precedence over JWT bearer.
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;

            // Length does far more for password strength than symbol
            // requirements, which mostly push people towards "Password1!".
            // NIST SP 800-63B makes the same recommendation.
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;

            // Per-account brute-force defence. Rate limiting handles per-IP.
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;

            // No email sender wired up yet, so requiring confirmation would lock
            // every new account out. Turn this on with the booking emails.
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>();

        // AddDefaultTokenProviders() is deliberately absent: it lives in the
        // ASP.NET Core shared framework, and its providers only matter for email
        // confirmation and password reset. Add it — along with a framework
        // reference — when those flows arrive.

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }

    private static IServiceCollection AddJwtOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ValidateOnStart turns a missing or too-short signing key into a clear
        // failure at boot rather than a confusing 500 on the first login.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
