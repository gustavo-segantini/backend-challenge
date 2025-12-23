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

    /// <summary>
    /// Checks if a line with the given hash has been previously processed.
    /// </summary>
    /// <param name="lineHash">SHA256 hash of the line content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the line is new (not previously processed), false if duplicate.</returns>
    Task<bool> IsLineUniqueAsync(string lineHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a processed line hash to track duplicate lines across uploads.
    /// Adds the line hash to the change tracker but does not save immediately.
    /// Use CommitLineHashesAsync to save all pending line hashes in a single database operation.
    /// </summary>
    /// <param name="fileUploadId">The parent FileUpload ID.</param>
    /// <param name="lineHash">SHA256 hash of the line content.</param>
    /// <param name="lineContent">The original line content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordLineHashAsync(Guid fileUploadId, string lineHash, string lineContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits all pending line hash records to the database in a single operation.
    /// Should be called after all line hashes have been added via RecordLineHashAsync.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous save operation</returns>
    Task CommitLineHashesAsync(CancellationToken cancellationToken = default);
}
