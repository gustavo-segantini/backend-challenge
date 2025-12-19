using CnabApi.Common;

namespace CnabApi.Services;

/// <summary>
/// Service responsible for orchestrating the complete CNAB file upload workflow.
/// Handles file reading, parsing, and storing transactions in a single operation.
/// </summary>
public interface ICnabUploadService
{
    /// <summary>
    /// Processes a CNAB file upload end-to-end.
    /// Reads the file, parses its content, and stores transactions in the database.
    /// </summary>
    /// <param name="file">The uploaded CNAB file.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing the count of imported transactions or error message.</returns>
    Task<Result<int>> ProcessCnabUploadAsync(IFormFile file, CancellationToken cancellationToken = default);
}
