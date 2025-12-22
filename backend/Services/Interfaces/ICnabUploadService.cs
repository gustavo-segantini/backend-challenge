using CnabApi.Common;

namespace CnabApi.Services;

/// <summary>
/// Service responsible for orchestrating the complete CNAB file upload workflow.
/// Handles file reading, parsing, and storing transactions in a single operation.
/// </summary>
public interface ICnabUploadService
{
    /// <summary>
    /// Processes CNAB file content from a string using streaming multipart reader.
    /// Accepts the already-read file content as a string for memory-efficient processing.
    /// Supports files up to 1GB without loading entire content into memory at once.
    /// </summary>
    /// <param name="fileContent">The file content as a string.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing the count of imported transactions or error message.</returns>
    Task<Result<int>> ProcessCnabUploadAsync(string fileContent, CancellationToken cancellationToken = default);
}
