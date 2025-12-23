namespace CnabApi.Models;

/// <summary>
/// Represents a single line hash from a file upload for tracking duplicate lines.
/// Allows detection of duplicate transaction lines across multiple file uploads.
/// </summary>
public class FileUploadLineHash
{
    public Guid Id { get; set; }

    /// <summary>
    /// SHA256 hash of the line content.
    /// </summary>
    public string LineHash { get; set; } = string.Empty;

    /// <summary>
    /// The original line content (for audit/debugging purposes).
    /// </summary>
    public string LineContent { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the parent FileUpload.
    /// </summary>
    public Guid FileUploadId { get; set; }

    /// <summary>
    /// The parent FileUpload entity.
    /// </summary>
    public FileUpload FileUpload { get; set; } = null!;

    /// <summary>
    /// When this line was processed.
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
