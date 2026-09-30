using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WalletApp.Data;

namespace WalletApp.Tests.Integration;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    private readonly string _dbName = "TestDb_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core related registrations from the API
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AppDbContext) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>))
            ).ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            // Also remove any remaining EF Core infrastructure that mentions SqlServer
            var efDescriptors = services.Where(d =>
                d.ServiceType.Namespace != null &&
                d.ServiceType.Namespace.StartsWith("Microsoft.EntityFrameworkCore") &&
                d.ServiceType != typeof(DbContextOptions<AppDbContext>)
            ).ToList();

            foreach (var descriptor in efDescriptors)
                services.Remove(descriptor);

            // Now register the in-memory database
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}