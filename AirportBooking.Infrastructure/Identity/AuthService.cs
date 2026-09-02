using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Auth;
using AirportBooking.Application.Interfaces;
using AirportBooking.Domain.Entities;
using AirportBooking.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AirportBooking.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly AppDbContext _db;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        AppDbContext db,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _db = db;
        _logger = logger;
    }

    public async Task<Result<AuthSession>> RegisterAsync(
        RegisterRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return Result<AuthSession>.Failure(Error.EmailAlreadyRegistered);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PreferredLanguage = request.PreferredLanguage
        };

        var created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            // Identity's own policy rejected it. FluentValidation should have
            // caught this first, so anything landing here is a rule the two
            // disagree on — worth surfacing rather than swallowing.
            var message = string.Join(" ", created.Errors.Select(e => e.Description));
            return Result<AuthSession>.Failure(new Error("auth.registration_failed", message));
        }

        _logger.LogInformation("Registered user {UserId}.", user.Id);

        return Result<AuthSession>.Success(await IssueSessionAsync(user, ipAddress, cancellationToken));
    }

    public async Task<Result<AuthSession>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim());

        // Unknown email and wrong password return the same error. Telling them
        // apart hands an attacker a way to enumerate who has an account.
        if (user is null)
        {
            return Result<AuthSession>.Failure(Error.InvalidCredentials);
        }

        // Checked before verifying the password, so a locked account costs an
        // attacker nothing to discover and gains them nothing either.
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login blocked for locked-out user {UserId}.", user.Id);
            return Result<AuthSession>.Failure(Error.AccountLocked);
        }

        // SignInManager would normally do this, but it lives in the ASP.NET Core
        // shared framework and this project is a class library. UserManager
        // exposes the same three primitives, and since the API is stateless
        // there is no sign-in cookie to create anyway.
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);

            // The failure just now may have been the one that tripped the limit.
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("User {UserId} locked out after repeated failures.", user.Id);
                return Result<AuthSession>.Failure(Error.AccountLocked);
            }

            return Result<AuthSession>.Failure(Error.InvalidCredentials);
        }

        // A successful sign-in clears the counter, so occasional typos spread
        // over time never accumulate into a lockout.
        await _userManager.ResetAccessFailedCountAsync(user);

        return Result<AuthSession>.Success(await IssueSessionAsync(user, ipAddress, cancellationToken));
    }

    public async Task<Result<AuthSession>> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var hash = _tokenService.Hash(refreshToken);

        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return Result<AuthSession>.Failure(Error.InvalidRefreshToken);
        }

        // A token that exists but is already revoked means someone is replaying
        // one that was rotated out. Either it was stolen or the chain leaked, and
        // there is no way to tell which holder is the legitimate one — so end
        // every session for this user and make them sign in again.
        if (stored.IsRevoked)
        {
            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoking all active tokens.",
                stored.UserId);

            await RevokeAllForUserAsync(stored.UserId, cancellationToken);
            return Result<AuthSession>.Failure(Error.InvalidRefreshToken);
        }

        if (stored.IsExpired)
        {
            return Result<AuthSession>.Failure(Error.InvalidRefreshToken);
        }

        var session = await IssueSessionAsync(stored.User, ipAddress, cancellationToken, rotatedFrom: stored);

        return Result<AuthSession>.Success(session);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = _tokenService.Hash(refreshToken);

        var stored = await _db.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        // An unknown or already-revoked token is not an error: logout should
        // always look like it worked.
        if (stored is null || stored.IsRevoked)
        {
            return;
        }

        stored.Revoke();
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Issues an access token plus a fresh refresh token, and — when this is a
    /// rotation — revokes the old one, recording which token replaced it so the
    /// chain can be walked during a reuse investigation.
    /// </summary>
    private async Task<AuthSession> IssueSessionAsync(
        ApplicationUser user,
        string? ipAddress,
        CancellationToken cancellationToken,
        RefreshToken? rotatedFrom = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, expiresAt) = _tokenService.CreateAccessToken(user, roles);

        var refresh = _tokenService.CreateRefreshToken();
        var refreshHash = _tokenService.Hash(refresh.Token);

        rotatedFrom?.Revoke(refreshHash);

        _db.RefreshTokens.Add(new RefreshToken(user.Id, refreshHash, refresh.ExpiresAtUtc, ipAddress));

        await _db.SaveChangesAsync(cancellationToken);

        var response = new AuthResponse(
            accessToken,
            expiresAt,
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.PreferredLanguage));

        return new AuthSession(response, refresh);
    }

    private async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.Revoke();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
