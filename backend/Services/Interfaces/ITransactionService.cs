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
    /// Does not invalidate cache - caller should handle that after commit.
    /// </summary>
    /// <param name="transaction">The transaction to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with the transaction if valid, or error.</returns>
    Task<Result<Transaction>> AddTransactionToContextAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cache for a specific CPF.
    /// </summary>
    Task InvalidateCacheForCpfAsync(string cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves transactions for a specific CPF with pagination and filters.
    /// </summary>
    Task<Result<PagedResult<Transaction>>> GetTransactionsByCpfAsync(TransactionQueryOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the total balance for a specific CPF.
    /// </summary>
    Task<Result<decimal>> GetBalanceByCpfAsync(string cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all transactions from the database.
    /// </summary>
    Task<Result> ClearAllDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches transactions by description using full-text search.
    /// </summary>
    Task<Result<PagedResult<Transaction>>> SearchTransactionsByDescriptionAsync(
        string cpf,
        string searchTerm,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
