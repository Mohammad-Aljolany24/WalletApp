using Asp.Versioning;

namespace WalletApp.API.Versioning;

public static class ApiVersioningExtensions
{
    /// <summary>
    /// Registers URL-segment API versioning (/api/v1/...) and the API Explorer
    /// integration that Swashbuckle reads to emit one OpenAPI document per version.
    /// </summary>
    public static IServiceCollection AddApiVersioningSupport(
        this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            // "'v'VVV" → v1, v1.5, v2, ...
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}