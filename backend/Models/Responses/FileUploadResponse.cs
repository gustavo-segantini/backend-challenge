namespace CnabApi.Models.Responses;

/// <summary>
/// Response model for file upload information.
/// Represents a file upload with processing status and progress information.
/// </summary>
public class FileUploadResponse
{
    /// <summary>
    /// Unique identifier for the file upload.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Original filename as provided by the client.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the upload (Pending, Processing, Success, Failed, etc.).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Total number of lines in the file.
    /// </summary>
    public int TotalLineCount { get; set; }

    /// <summary>
    /// Number of transaction lines successfully processed.
    /// </summary>
    public int ProcessedLineCount { get; set; }

    /// <summary>
    /// Number of lines that failed to process.
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
    /// Timestamp when processing started (UTC).
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }

    /// <summary>
    /// Timestamp when processing completed (UTC).
    /// </summary>
    public DateTime? ProcessingCompletedAt { get; set; }

    /// <summary>
    /// Timestamp when the file was uploaded (UTC).
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Number of retry attempts for background processing.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Optional error message if the upload failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Optional path or reference to the file in object storage.
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Progress percentage (0-100) based on processed, failed, and skipped lines.
    /// </summary>
    public double ProgressPercentage { get; set; }
}

