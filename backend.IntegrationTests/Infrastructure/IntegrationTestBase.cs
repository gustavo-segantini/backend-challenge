using CnabApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CnabApi.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that use the database
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private DatabaseFixture? _databaseFixture;
    
    protected DatabaseFixture DatabaseFixture =>
        _databaseFixture ?? throw new InvalidOperationException("DatabaseFixture not initialized");

    protected CnabDbContext DbContext => DatabaseFixture.CreateDbContext();

    async Task IAsyncLifetime.InitializeAsync()
    {
        _databaseFixture = new DatabaseFixture();
        await _databaseFixture.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_databaseFixture != null)
        {
            await _databaseFixture.DisposeAsync();
        }
    }

    /// <summary>
    /// Clear all data from tables (useful for test isolation)
    /// </summary>
    protected async Task ClearDatabaseAsync()
    {
        using var context = DbContext;
        
        var transactions = await context.Transactions.ToListAsync();
        context.Transactions.RemoveRange(transactions);

        await context.SaveChangesAsync();
    }
}
