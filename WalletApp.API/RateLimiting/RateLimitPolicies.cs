namespace WalletApp.API.RateLimiting;

public static class RateLimitPolicies
{
    /// <summary>Strict limiter for /auth/login and /auth/register.</summary>
    public const string Auth = "auth";

    /// <summary>
    /// Partition key for the global limiter. Authenticated callers are keyed
    /// on their user ID so a shared office NAT doesn't cause one user to
    /// throttle another. Anonymous callers fall back to IP.
    /// </summary>
    public static string GetPartitionKey(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(sub))
            return $"user:{sub}";

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ip}";
    }

    /// <summary>
    /// Partition key for auth endpoints. IP only — there is no user yet, and
    /// keying on the email/username would let an attacker rotate identifiers
    /// to bypass the limit.
    /// </summary>
    public static string GetAuthPartitionKey(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"auth:{ip}";
    }
}