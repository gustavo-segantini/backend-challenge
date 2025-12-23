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
    /// Number of transaction lines successfully processed from this file.
    /// </summary>
    public int ProcessedLineCount { get; set; }

    /// <summary>
    /// Status of the file upload (Success, Failed, Pending, etc.).
    /// </summary>
    public FileUploadStatus Status { get; set; } = FileUploadStatus.Success;

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

    /// <summary>Upload and processing completed successfully.</summary>
    Success = 1,

    /// <summary>Upload failed due to validation or processing errors.</summary>
    Failed = 2,

    /// <summary>Upload was rejected as duplicate.</summary>
    Duplicate = 3
}
