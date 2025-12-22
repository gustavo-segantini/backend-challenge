using CnabApi.Common;
using CnabApi.Models;

namespace CnabApi.Services.Facades;

/// <summary>
/// Facade service that orchestrates all transaction-related operations.
/// Encapsulates business logic and validation, keeping controllers thin.
/// </summary>
public interface ITransactionFacadeService
{
    /// <summary>
    /// Orchestrates the entire CNAB file upload process.
    /// Handles multipart reading, validation, parsing, and persistence.
    /// </summary>
    /// <param name="request">The HTTP request containing the multipart form data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with count of imported transactions and appropriate status code info.</returns>
    Task<Result<UploadResult>> UploadCnabFileAsync(HttpRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves transactions for a CPF with filtering and pagination.
    /// </summary>
    /// <param name="cpf">The CPF to filter transactions.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="types">Comma-separated nature codes.</param>
    /// <param name="sort">Sort direction ('asc' or 'desc').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with paged transactions.</returns>
    Task<Result<PagedResult<Transaction>>> GetTransactionsByCpfAsync(
        string cpf,
        int page,
        int pageSize,
        DateTime? startDate,
        DateTime? endDate,
        string? types,
        string sort,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calculates the total balance for a specific CPF.
    /// </summary>
    /// <param name="cpf">The CPF to calculate balance for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with the calculated balance.</returns>
    Task<Result<decimal>> GetBalanceByCpfAsync(string cpf, CancellationToken cancellationToken);

    /// <summary>
    /// Searches transactions by description for a specific CPF.
    /// </summary>
    /// <param name="cpf">The CPF to filter transactions.</param>
    /// <param name="searchTerm">The search term.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with matching transactions.</returns>
    Task<Result<PagedResult<Transaction>>> SearchTransactionsByDescriptionAsync(
        string cpf,
        string searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears all transactions and stores from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> ClearAllDataAsync(CancellationToken cancellationToken);
}
