using CnabApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CnabApi.Services.UnitOfWork;

/// <summary>
/// Entity Framework Core implementation of Unit of Work pattern.
/// Ensures ACID compliance by managing transactions across multiple operations.
/// </summary>
public class EfCoreUnitOfWork(CnabDbContext context) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("Transaction already started.");
        }

        _transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
            context.ChangeTracker.Clear();
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        // Check if we're using InMemory provider, which doesn't support transactions
        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            // For InMemory, execute the operation and save changes without transaction
            var result = await operation();
            await context.SaveChangesAsync(cancellationToken);
            return result;
        }

        // Start transaction if not already started
        if (_transaction == null)
        {
            await BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var result = await operation();
            
            // Only commit if we started the transaction here
            if (_transaction != null)
            {
                await CommitAsync(cancellationToken);
            }
            
            return result;
        }
        catch
        {
            if (_transaction != null)
            {
                await RollbackAsync(cancellationToken);
            }
            throw;
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        DisposeTransactionAsync().GetAwaiter().GetResult();
    }
}

