using CnabApi.Common;

namespace CnabApi.Services;

/// <summary>
/// Service responsible for orchestrating the complete CNAB file upload workflow.
/// Handles file reading, parsing, and storing transactions line by line.
/// Supports resumable processing with checkpoints.
/// </summary>
public interface ICnabUploadService
{
    /// <summary>
    /// Processes CNAB file content line by line with parallel processing.
    /// Each line is validated for duplicates and inserted atomically.
    /// Supports resuming from a checkpoint line index.
    /// </summary>
    /// <param name="fileContent">The file content as a string.</param>
    /// <param name="fileUploadId">The FileUpload ID for tracking.</param>
    /// <param name="startFromLine">Line index to start from (for checkpoint resume). Default: 0</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing the count of successfully imported transactions.</returns>
    Task<Result<int>> ProcessCnabUploadAsync(
        string fileContent, 
        Guid fileUploadId, 
        int startFromLine = 0, 
        CancellationToken cancellationToken = default);
}
