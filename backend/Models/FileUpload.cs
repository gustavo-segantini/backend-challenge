namespace CnabApi.Models;

/// <summary>
/// Represents a file upload record for tracking CNAB file uploads and detecting duplicates.
/// </summary>
public class FileUpload
{
    /// <summary>
    /// Primary key identifier for the file upload record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// SHA256 hash of the file content (used for duplicate detection).
    /// Unique constraint ensures no duplicate file content can be uploaded.
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// Original filename as provided by the client.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Upload timestamp (UTC).
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of lines in the file.
    /// </summary>
    public int TotalLineCount { get; set; }

    /// <summary>
    /// Number of transaction lines successfully processed from this file.
    /// </summary>
    public int ProcessedLineCount { get; set; }

    /// <summary>
    /// Number of lines that failed to process after retries.
    /// </summary>
    public int FailedLineCount { get; set; }

    /// <summary>
    /// Number of duplicate lines that were skipped.
    /// </summary>
    public int SkippedLineCount { get; set; }

    /// <summary>
    /// Last checkpoint line index (for resume support).
    /// </summary>
    public int LastCheckpointLine { get; set; }

    /// <summary>
    /// Timestamp of the last checkpoint update (UTC).
    /// </summary>
    public DateTime? LastCheckpointAt { get; set; }

    /// <summary>
    /// Status of the file upload (Success, Failed, Pending, etc.).
    /// </summary>
    public FileUploadStatus Status { get; set; } = FileUploadStatus.Pending;

    /// <summary>
    /// Timestamp when processing started (UTC).
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// Timestamp when processing completed (UTC).
    /// </summary>
    public DateTime? ProcessingCompletedAt { get; set; }

    /// <summary>
    /// Number of retry attempts for background processing.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Optional error message if the upload failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Optional path or reference to the file in MinIO storage.
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Collection of line hashes from this file for tracking individual line duplicates.
    /// </summary>
    public ICollection<FileUploadLineHash> LineHashes { get; set; } = new List<FileUploadLineHash>();
}

/// <summary>
/// Enumeration for file upload status.
/// </summary>
public enum FileUploadStatus
{
    /// <summary>Upload is pending processing.</summary>
    Pending = 0,

    /// <summary>Upload is currently being processed.</summary>
    Processing = 1,

    /// <summary>Upload and processing completed successfully.</summary>
    Success = 2,

    /// <summary>Upload failed due to validation or processing errors.</summary>
    Failed = 3,

    /// <summary>Upload was rejected as duplicate.</summary>
    Duplicate = 4,

    /// <summary>Processing completed but some lines failed.</summary>
    PartiallyCompleted = 5
}
