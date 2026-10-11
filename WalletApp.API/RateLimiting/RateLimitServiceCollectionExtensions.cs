using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WalletApp.API.RateLimiting;

public static class RateLimitServiceCollectionExtensions
{
    /// <summary>
    /// Registers the two rate-limit policies and the 429 response shape.
    /// The <c>RateLimiting:Enabled</c> flag is read per-request, not at
    /// registration time — this lets the integration test factory disable
    /// rate limiting via a config override that applies after Program.cs
    /// has already executed its service registrations.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            // Global limiter — applies to every request via middleware.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                if (!configuration.GetValue("RateLimiting:Enabled", true))
                    return RateLimitPartition.GetNoLimiter("disabled-global");

                var permitLimit = configuration.GetValue("RateLimiting:Global:PermitLimit", 100);
                var windowSeconds = configuration.GetValue("RateLimiting:Global:WindowSeconds", 60);

                return RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitPolicies.GetPartitionKey(ctx),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            // Auth policy — applied per-endpoint on login/register.
            options.AddPolicy(RateLimitPolicies.Auth, ctx =>
            {
                if (!configuration.GetValue("RateLimiting:Enabled", true))
                    return RateLimitPartition.GetNoLimiter("disabled-auth");

                var permitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", 5);
                var windowSeconds = configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);

                return RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitPolicies.GetAuthPartitionKey(ctx),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    Detail = "Rate limit exceeded. Please slow down and retry later.",
                    Type = "https://walletapp.local/errors/rate-limit"
                };
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            };
        });

        return services;
    }
}