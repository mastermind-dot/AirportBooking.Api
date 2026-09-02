using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AirportBooking.Application.DTOs.Auth;
using AirportBooking.Application.Interfaces;
using AirportBooking.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AirportBooking.Infrastructure.Identity;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),

            // A unique id per token, so an individual token could be
            // denylisted later without invalidating every session.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            new("given_name", user.FirstName),
            new("family_name", user.LastName),
            new("lang", user.PreferredLanguage)
        };

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _credentials
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return (token, expiresAt);
    }

    public RefreshTokenResult CreateRefreshToken()
    {
        // 64 bytes from the OS CSPRNG. Guessing one is not a realistic attack,
        // which is what lets the token be a bare random string rather than a
        // signed structure.
        var bytes = RandomNumberGenerator.GetBytes(64);

        return new RefreshTokenResult(
            Base64UrlEncoder.Encode(bytes),
            DateTime.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    /// <summary>
    /// Plain SHA-256, not a password hash. Deliberate: the token already has 512
    /// bits of entropy, so there is nothing to brute-force and no need to make
    /// verification slow — this runs on every token refresh.
    /// </summary>
    public string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
