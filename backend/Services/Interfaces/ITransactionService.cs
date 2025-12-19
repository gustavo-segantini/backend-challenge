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
}
