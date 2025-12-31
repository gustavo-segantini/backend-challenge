using CnabApi.Common;
using CnabApi.Models;

namespace CnabApi.Services.Interfaces;

/// <summary>
/// Strategy interface for processing uploaded CNAB files.
/// Allows different processing strategies (synchronous for tests, asynchronous for production)
/// to be injected via dependency injection, following the Strategy pattern and Dependency Inversion Principle.
/// </summary>
public interface IUploadProcessingStrategy
{
    /// <summary>
    /// Processes an uploaded CNAB file according to the strategy implementation.
    /// </summary>
    /// <param name="fileContent">The content of the CNAB file to process.</param>
    /// <param name="fileUploadRecord">The file upload record tracking this upload.</param>
    /// <param name="storagePath">Optional storage path where the file is stored.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing the upload result with transaction count and status code.</returns>
    Task<Result<UploadResult>> ProcessUploadAsync(
        string fileContent,
        FileUpload fileUploadRecord,
        string? storagePath,
        CancellationToken cancellationToken = default);
}

