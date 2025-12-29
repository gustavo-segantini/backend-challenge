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
    /// Clears all transactions and stores from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> ClearAllDataAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets transactions grouped by store name and owner, with balance calculated for each store.
    /// </summary>
    /// <param name="uploadId">Optional upload ID to filter transactions by a specific file upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with list of grouped transactions by store.</returns>
    Task<Result<List<StoreGroupedTransactions>>> GetTransactionsGroupedByStoreAsync(
        Guid? uploadId = null,
        CancellationToken cancellationToken = default);
}
