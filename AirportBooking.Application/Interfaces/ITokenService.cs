using AirportBooking.Application.DTOs.Auth;
using AirportBooking.Domain.Entities;

namespace AirportBooking.Application.Interfaces;

public interface ITokenService
{
    /// <summary>
    /// Signs a short-lived JWT. Short-lived because there is no way to revoke a
    /// JWT once issued — the only real control is how quickly it expires.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>Generates a cryptographically random refresh token. The raw value is returned once and never stored.</summary>
    RefreshTokenResult CreateRefreshToken();

    /// <summary>
    /// SHA-256 of a raw refresh token. Only the hash is persisted, so a leaked
    /// database cannot be replayed against the API.
    /// </summary>
    string Hash(string rawToken);
}
