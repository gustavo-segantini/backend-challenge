using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using CnabApi.Data;

namespace CnabApi.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration testing with in-memory database.
/// </summary>
public class CnabApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configure test environment - Program.cs will use InMemory database for Test environment
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Remove any existing IDistributedCache registrations
            var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            if (cacheDescriptor is not null)
            {
                services.Remove(cacheDescriptor);
            }

            // Add in-memory distributed cache for testing
            services.AddDistributedMemoryCache();
        });
    }

    public HttpClient CreateClientWithCleanDatabase()
    {
        var dbName = $"TestDatabase_{Guid.NewGuid()}";

        var factory = WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CnabDbContext>));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<CnabDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));
                
                // Remove any existing IDistributedCache registrations
                var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
                if (cacheDescriptor is not null)
                {
                    services.Remove(cacheDescriptor);
                }

                // Add in-memory distributed cache for testing
                services.AddDistributedMemoryCache();
            });
        });

        return factory.CreateClient();
    }
}
