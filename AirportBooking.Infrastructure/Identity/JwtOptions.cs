using System.ComponentModel.DataAnnotations;

namespace AirportBooking.Infrastructure.Identity;

/// <summary>
/// Bound from the "Jwt" configuration section. Everything here except
/// <see cref="SigningKey"/> lives in appsettings.json; the key comes from
/// user-secrets in development and from the environment or a vault in production.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 needs at least 256 bits of key. Anything shorter is rejected
    /// at startup rather than quietly weakening every token.
    /// </summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Short by design. A JWT cannot be revoked once issued, so the expiry is
    /// the only thing limiting the damage from a stolen token.
    /// </summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;

    public string RefreshCookieName { get; set; } = "skybook_rt";
}
