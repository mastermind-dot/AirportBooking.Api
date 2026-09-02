using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Auth;

namespace AirportBooking.Application.Interfaces;

/// <summary>
/// Registration and sign-in. Implemented in Infrastructure, which owns the
/// Identity and EF Core dependencies, so this layer stays free of both.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthSession>> RegisterAsync(
        RegisterRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<AuthSession>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the presented refresh token, revokes it, and issues a
    /// replacement. Rotation on every use means a stolen token stops working as
    /// soon as the real client refreshes — and the attempt to reuse it is
    /// detectable.
    /// </summary>
    Task<Result<AuthSession>> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes the presented token. Idempotent: an unknown token is not an error.</summary>
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
}
