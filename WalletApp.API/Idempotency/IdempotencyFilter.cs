using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Data.Entities;

namespace WalletApp.API.Idempotency;

/// <summary>
/// Endpoint filter that makes a POST endpoint idempotent when the client
/// supplies an <c>Idempotency-Key</c> header.
///
/// Behaviour:
///  - No header  → pass through (backwards compatible).
///  - Header present, key unseen → execute handler, cache response.
///  - Header present, key seen   → return cached response (no re-execution).
///  - Header present, key seen for a different endpoint → 422.
///
/// Cache is keyed on (UserId, Key). TTL is 24h; a background job will
/// purge expired rows in Phase 2.5.
/// </summary>
public class IdempotencyFilter : IEndpointFilter
{
    private const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 200;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly AppDbContext _db;

    public IdempotencyFilter(AppDbContext db) => _db = db;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        if (!http.Request.Headers.TryGetValue(HeaderName, out var values))
            return await next(context);

        var key = values.ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
        {
            return Results.Problem(
                title: "Invalid Idempotency-Key.",
                detail: $"Header must be a non-empty string of at most {MaxKeyLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = GetUserId(http);
        if (userId is null)
        {
            return Results.Problem(
                title: "Unauthorized.",
                detail: "Missing or invalid user identity.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var endpoint = http.Request.Path.Value ?? "";

        var existing = await _db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key);

        if (existing is not null)
        {
            if (!string.Equals(existing.Endpoint, endpoint, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    title: "Idempotency-Key reuse.",
                    detail: $"This key was already used for {existing.Endpoint}.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            http.Response.StatusCode = existing.StatusCode;
            return Results.Text(existing.ResponseBody, "application/json");
        }

        var result = await next(context);
        if (result is not IResult innerResult)
            return result;

        var record = new IdempotencyRecord
        {
            UserId = userId.Value,
            Key = key,
            Endpoint = endpoint,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(Ttl),
        };

        return new CapturingResult(innerResult, async (status, body) =>
        {
            record.StatusCode = status;
            record.ResponseBody = body;
            _db.IdempotencyRecords.Add(record);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request with the same key won the race.
                // Its response is now cached; ours stands as-is.
            }
        });
    }

    private static Guid? GetUserId(HttpContext http)
    {
        var sub = http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>
    /// Wraps the endpoint's IResult so we can tee the response body
    /// into a MemoryStream, read it for caching, and forward it on.
    /// The pipeline only executes this wrapper once — no double-write.
    /// </summary>
    private sealed class CapturingResult : IResult
    {
        private readonly IResult _inner;
        private readonly Func<int, string, Task> _capture;

        public CapturingResult(IResult inner, Func<int, string, Task> capture)
        {
            _inner = inner;
            _capture = capture;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var originalBody = httpContext.Response.Body;
            using var buffer = new MemoryStream();
            httpContext.Response.Body = buffer;

            try
            {
                await _inner.ExecuteAsync(httpContext);

                buffer.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(buffer, leaveOpen: true);
                var bodyText = await reader.ReadToEndAsync();
                var status = httpContext.Response.StatusCode;

                httpContext.Response.Body = originalBody;
                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(originalBody);

                await _capture(status, bodyText);
            }
            finally
            {
                httpContext.Response.Body = originalBody;
            }
        }
    }
}