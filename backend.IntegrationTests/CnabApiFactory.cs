using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            });
        });

        return factory.CreateClient();
    }
}
