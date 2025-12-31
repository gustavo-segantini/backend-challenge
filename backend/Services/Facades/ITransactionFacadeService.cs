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
    /// <param name="page">Page number (1-based). Default: 1.</param>
    /// <param name="pageSize">Number of items per page. Default: 50.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with paginated list of grouped transactions by store.</returns>
    Task<Result<Models.Responses.PagedResponse<StoreGroupedTransactions>>> GetTransactionsGroupedByStoreAsync(
        Guid? uploadId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
