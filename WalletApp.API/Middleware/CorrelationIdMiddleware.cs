using Serilog.Context;

namespace WalletApp.API.Middleware;

/// <summary>
/// Assigns every request a correlation ID and pushes it into the Serilog
/// LogContext so every log line emitted during the request carries it.
///
/// If the client sends <c>X-Correlation-Id</c>, that value is used —
/// useful when an upstream service wants to trace a call across systems.
/// Otherwise the ASP.NET Core <see cref="HttpContext.TraceIdentifier"/> is used,
/// which is already what ProblemDetails returns as <c>traceId</c>.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var values)
            && !string.IsNullOrWhiteSpace(values)
                ? values.ToString()
                : context.TraceIdentifier;

        // Make ProblemDetails' traceId and our log correlationId the same value.
        context.TraceIdentifier = correlationId;

        // Echo it back so the client can attach it to bug reports.
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}