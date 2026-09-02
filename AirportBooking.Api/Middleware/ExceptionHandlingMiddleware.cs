using System.Diagnostics;
using AirportBooking.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AirportBooking.Api.Middleware;

/// <summary>
/// Turns unhandled exceptions into RFC 7807 problem+json.
///
/// The rule it enforces: a client learns the shape of the failure, never the
/// internals. Stack traces, SQL and connection strings stay in the log, which is
/// where whoever is on call can actually use them.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // A broken business rule: the request was well-formed but the action
            // is not allowed right now. 409 rather than 500 — nothing is wrong
            // with the server, and these messages are written to be user-facing.
            _logger.LogInformation(ex, "Domain rule violated: {Message}", ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Request could not be completed", ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client hung up. Not an error, and logging it as one turns a
            // user closing a tab into noise in the error dashboard.
            _logger.LogDebug("Request cancelled by the client.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                _environment.IsDevelopment()
                    ? ex.ToString()
                    : "Something went wrong. Please try again.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            // Too late to replace the response; the headers are already on the
            // wire. Overwriting here would corrupt the body the client is reading.
            return;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        // Correlates the sanitised client response with the full log entry.
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }
}
