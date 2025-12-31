using CnabApi.Common;
using CnabApi.Models;

namespace CnabApi.Services;

/// <summary>
/// Service for managing transactions.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Adds transactions to the database.
    /// </summary>
    Task<Result<List<Transaction>>> AddTransactionsAsync(List<Transaction> transactions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a single transaction atomically with immediate commit.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with the added transaction or error.</returns>
    Task<Result<Transaction>> AddSingleTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a single transaction to the context without saving (for use with Unit of Work).
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with the transaction if valid, or error.</returns>
    Task<Result<Transaction>> AddTransactionToContextAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all transactions from the database.
    /// </summary>
    Task<Result> ClearAllDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets transactions grouped by store name and owner, with balance calculated for each store.
    /// </summary>
    /// <param name="uploadId">Optional upload ID to filter transactions by a specific file upload.</param>
    /// <param name="page">Page number (1-based). Default: 1.</param>
    /// <param name="pageSize">Number of items per page. Default: 50.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<Models.Responses.PagedResponse<StoreGroupedTransactions>>> GetTransactionsGroupedByStoreAsync(
        Guid? uploadId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
