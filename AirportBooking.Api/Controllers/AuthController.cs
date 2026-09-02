using System.Security.Claims;
using AirportBooking.Api.Extensions;
using AirportBooking.Application.Common;
using AirportBooking.Application.DTOs.Auth;
using AirportBooking.Application.Interfaces;
using AirportBooking.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AirportBooking.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(ApiPolicies.AuthRateLimit)]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(IAuthService authService, IOptions<JwtOptions> jwtOptions)
    {
        _authService = authService;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, ClientIp(), cancellationToken);
        return HandleSession(result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, ClientIp(), cancellationToken);
        return HandleSession(result);
    }

    /// <summary>
    /// Exchanges the refresh cookie for a new access token. Takes no body — the
    /// browser attaches the cookie on its own, and accepting the token in a body
    /// would let a script that can read it use it.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var token = Request.Cookies[_jwtOptions.RefreshCookieName];

        if (string.IsNullOrEmpty(token))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Not signed in",
                detail: Error.InvalidRefreshToken.Message);
        }

        var result = await _authService.RefreshAsync(token, ClientIp(), cancellationToken);

        if (result.IsFailure)
        {
            // The cookie is dead either way; clearing it stops the client
            // retrying with a token that can never work again.
            ClearRefreshCookie();
        }

        return HandleSession(result);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(Request.Cookies[_jwtOptions.RefreshCookieName], cancellationToken);

        ClearRefreshCookie();

        return NoContent();
    }

    /// <summary>The signed-in user, read from the validated token rather than the database.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(id, out var userId))
        {
            return Unauthorized();
        }

        return Ok(new UserDto(
            userId,
            User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty,
            User.FindFirstValue("given_name") ?? string.Empty,
            User.FindFirstValue("family_name") ?? string.Empty,
            User.FindFirstValue("lang") ?? "en"));
    }

    private IActionResult HandleSession(Result<AuthSession> result)
    {
        if (result.IsFailure)
        {
            var error = result.Error!;

            var status = error.Code switch
            {
                "auth.email_taken" => StatusCodes.Status409Conflict,
                "auth.locked_out" => StatusCodes.Status423Locked,
                "auth.registration_failed" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status401Unauthorized
            };

            var problem = Problem(statusCode: status, title: "Authentication failed", detail: error.Message);

            if (problem is ObjectResult { Value: ProblemDetails details })
            {
                details.Extensions["code"] = error.Code;
            }

            return problem;
        }

        var session = result.Value!;
        SetRefreshCookie(session.RefreshToken);

        return Ok(session.Response);
    }

    private void SetRefreshCookie(RefreshTokenResult refreshToken)
    {
        Response.Cookies.Append(_jwtOptions.RefreshCookieName, refreshToken.Token, new CookieOptions
        {
            // Unreadable from JavaScript, so an XSS cannot steal the session.
            HttpOnly = true,

            // HTTPS only. The dev cert covers localhost, so this holds locally too.
            Secure = true,

            // Not sent on cross-site requests at all, which rules out CSRF
            // against the refresh endpoint. Locally the Vite proxy keeps the SPA
            // and API same-origin; in production, host them on the same site or
            // this has to relax to SameSite=None.
            SameSite = SameSiteMode.Strict,

            // The only endpoints that need it. A cookie scoped to "/" would ride
            // along on every request for no reason.
            Path = "/api/auth",

            Expires = refreshToken.ExpiresAtUtc
        });
    }

    private void ClearRefreshCookie()
    {
        // Every attribute except Expires must match the original, or the browser
        // treats this as a different cookie and leaves the real one in place.
        Response.Cookies.Append(_jwtOptions.RefreshCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = DateTimeOffset.UnixEpoch
        });
    }

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
