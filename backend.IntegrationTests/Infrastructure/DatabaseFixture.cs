using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using CnabApi.Data;

namespace CnabApi.IntegrationTests.Infrastructure;

/// <summary>
/// Fixture for managing PostgreSQL test container lifecycle
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string? _connectionString;

    public string ConnectionString 
    {
        get => _connectionString ?? throw new InvalidOperationException("Connection string not initialized");
    }

    public DatabaseFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("cnab_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithCleanUp(true)
            .Build();
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Apply migrations
        using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Public initialization for explicit control
    /// </summary>
    public async Task InitializeAsync()
    {
        await ((IAsyncLifetime)this).InitializeAsync();
    }

    /// <summary>
    /// Public disposal for explicit control
    /// </summary>
    public async Task DisposeAsync()
    {
        await ((IAsyncLifetime)this).DisposeAsync();
    }

    /// <summary>
    /// Create a fresh DbContext with the test database
    /// </summary>
    public CnabDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CnabDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new CnabDbContext(options);
    }
}
