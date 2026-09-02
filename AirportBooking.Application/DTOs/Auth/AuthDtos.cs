namespace AirportBooking.Application.DTOs.Auth;

public sealed record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string PreferredLanguage = "en");

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string PreferredLanguage);

/// <summary>
/// What the client receives on a successful register, login or refresh.
///
/// The refresh token is deliberately absent: it goes back as an HttpOnly cookie
/// the browser attaches automatically and JavaScript cannot read. Putting it in
/// this body would hand it to any script running on the page.
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserDto User);

/// <summary>
/// A freshly issued refresh token, travelling from the service to the controller
/// that writes the cookie. Never serialised into a response body.
/// </summary>
public sealed record RefreshTokenResult(string Token, DateTime ExpiresAtUtc);

/// <summary>Pairs the JSON body with the cookie value the controller must set.</summary>
public sealed record AuthSession(AuthResponse Response, RefreshTokenResult RefreshToken);
