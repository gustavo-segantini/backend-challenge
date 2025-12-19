using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
        return CreateClient();
    }
}
