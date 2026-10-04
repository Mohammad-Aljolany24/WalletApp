using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WalletApp.Core.EventStore;
using WalletApp.Core.Exceptions;

namespace WalletApp.API.Exceptions;

/// <summary>
/// Single place that maps thrown exceptions to HTTP responses.
/// Replaces per-endpoint try/catch. Returns RFC 7807 ProblemDetails.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = Map(exception);

        // Domain exceptions are expected failures — log at Information.
        // Everything else is a bug — log at Error with the full stack.
        if (exception is DomainException or ConcurrencyException)
        {
            _logger.LogInformation(
                "Handled {Type}: {Message}",
                exception.GetType().Name,
                exception.Message);
        }
        else
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode =
            problem.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    // Order matters. InsufficientFundsException derives from ValidationException,
    // so it must appear first or it will never match.
    private static ProblemDetails Map(Exception exception) => exception switch
    {
        InsufficientFundsException insufficient => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Insufficient funds.",
            Detail = $"You requested {insufficient.Requested}, but only {insufficient.Available} is available.",
            Type = "https://walletapp.local/errors/insufficient-funds",
        },
        ValidationException validation => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed.",
            Detail = validation.Message,
            Type = "https://walletapp.local/errors/validation",
        },
        NotFoundException notFound => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not found.",
            Detail = notFound.Message,
            Type = "https://walletapp.local/errors/not-found",
        },
        UnauthorizedException unauthorized => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized.",
            Detail = unauthorized.Message,
            Type = "https://walletapp.local/errors/unauthorized",
        },
        ConcurrencyException concurrency => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Concurrency conflict.",
            Detail = concurrency.Message,
            Type = "https://walletapp.local/errors/concurrency",
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            // Detail intentionally omitted — internal messages must not leak.
            Type = "https://walletapp.local/errors/internal",
        },
    };
}