using CnabApi.Models;

namespace CnabApi.Services.Interfaces;

/// <summary>
/// Service for tracking file uploads and detecting duplicate uploads.
/// </summary>
public interface IFileUploadTrackingService
{
    /// <summary>
    /// Checks if a file with the given hash has been previously uploaded.
    /// </summary>
    /// <param name="fileHash">SHA256 hash of the file content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple with (IsUnique, ExistingUpload). IsUnique=true if file is new.</returns>
    Task<(bool IsUnique, FileUpload? ExistingUpload)> IsFileUniqueAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful file upload in the database.
    /// </summary>
    /// <param name="fileName">Original filename.</param>
    /// <param name="fileHash">SHA256 hash of the file content.</param>
    /// <param name="fileSize">File size in bytes.</param>
    /// <param name="processedLineCount">Number of lines processed.</param>
    /// <param name="storagePath">Path to the file in storage (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created FileUpload record.</returns>
    Task<FileUpload> RecordSuccessfulUploadAsync(
        string fileName,
        string fileHash,
        long fileSize,
        int processedLineCount,
        string? storagePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed file upload in the database.
    /// </summary>
    /// <param name="fileName">Original filename.</param>
    /// <param name="fileHash">SHA256 hash of the file content.</param>
    /// <param name="fileSize">File size in bytes.</param>
    /// <param name="errorMessage">Error message describing the failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created FileUpload record.</returns>
    Task<FileUpload> RecordFailedUploadAsync(
        string fileName,
        string fileHash,
        long fileSize,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the SHA256 hash of a stream content.
    /// </summary>
    /// <param name="stream">The stream to hash.</param>
    /// <returns>SHA256 hash as a hexadecimal string.</returns>
    Task<string> CalculateFileHashAsync(Stream stream);
}
