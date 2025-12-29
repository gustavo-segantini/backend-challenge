using System.Data;

namespace CnabApi.Services.UnitOfWork;

/// <summary>
/// Unit of Work pattern for managing database transactions across multiple repositories.
/// Ensures ACID compliance by grouping related operations into a single transaction.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation within a transaction.
    /// Automatically commits on success or rolls back on failure.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}

