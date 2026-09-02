namespace AirportBooking.Application.Common;

/// <summary>
/// A failure that the caller is expected to handle. Wrong credentials are not
/// exceptional — they are the normal outcome of a login form — so they travel
/// as a value rather than a thrown exception.
/// </summary>
/// <param name="Code">Stable, machine-readable. The API maps it to a status code.</param>
/// <param name="Message">Safe to show a user. Never leaks whether an account exists.</param>
public sealed record Error(string Code, string Message)
{
    public static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "Email or password is incorrect.");

    public static readonly Error EmailAlreadyRegistered =
        new("auth.email_taken", "That email address is already registered.");

    public static readonly Error AccountLocked =
        new("auth.locked_out", "Too many failed attempts. Try again later.");

    public static readonly Error InvalidRefreshToken =
        new("auth.invalid_refresh_token", "Your session has expired. Please sign in again.");
}

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public Error? Error { get; }
    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? value, Error? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static new Result<T> Failure(Error error) => new(false, default, error);
}
