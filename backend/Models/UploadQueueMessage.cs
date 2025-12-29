namespace CnabApi.Models;

/// <summary>
/// Message model for upload queue in Redis Streams.
/// Contains all information needed to process the upload in the background.
/// </summary>
public class UploadQueueMessage
{
    /// <summary>
    /// Unique identifier for the file upload record.
    /// </summary>
    public Guid UploadId { get; set; }

    /// <summary>
    /// Original filename as provided by the client.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The file content to process.
    /// </summary>
    public string FileContent { get; set; } = string.Empty;

    /// <summary>
    /// When the message was enqueued (UTC).
    /// </summary>
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of retry attempts (incremented each time message is reprocessed).
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of retry attempts before moving to dead letter queue.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Last error message from a failed processing attempt.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When the last processing attempt was made (UTC).
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }
}
