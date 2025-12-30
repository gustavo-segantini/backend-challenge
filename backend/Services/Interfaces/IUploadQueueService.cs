using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Services.Interfaces;

/// <summary>
/// Service for managing file upload queue using Redis Streams.
/// Handles enqueuing, dequeuing, and moving to dead letter queue.
/// </summary>
public interface IUploadQueueService
{
    /// <summary>
    /// Enqueues a file upload for background processing.
    /// Only sends a reference (uploadId + storagePath) - not the file content.
    /// </summary>
    /// <param name="uploadId">The FileUpload record ID</param>
    /// <param name="storagePath">Path to the file in MinIO storage</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The message ID in the stream</returns>
    Task<string> EnqueueUploadAsync(Guid uploadId, string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next upload from the queue (consumer group pattern).
    /// Returns only the reference - file must be fetched from MinIO.
    /// </summary>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="consumerId">Consumer ID within the group</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple with (messageId, uploadId, storagePath) or null if queue is empty</returns>
    Task<(string MessageId, Guid UploadId, string StoragePath)?> DequeueUploadAsync(
        string consumerGroup,
        string consumerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges successful processing of a message.
    /// </summary>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="messageId">Message ID to acknowledge</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AcknowledgeMessageAsync(string consumerGroup, string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a message to the dead letter queue when processing fails.
    /// </summary>
    /// <param name="messageId">Message ID to move</param>
    /// <param name="uploadId">The FileUpload record ID</param>
    /// <param name="reason">Reason for moving to DLQ</param>
    /// <param name="retryCount">Number of retries attempted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MoveToDeadLetterQueueAsync(
        string messageId,
        Guid uploadId,
        string reason,
        int retryCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets queue statistics (pending messages, consumer groups, etc).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue statistics</returns>
    Task<UploadQueueStats> GetQueueStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the consumer group if it doesn't exist.
    /// </summary>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeConsumerGroupAsync(string consumerGroup, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about the upload queue.
/// </summary>

[ExcludeFromCodeCoverage]
public class UploadQueueStats
{
    public long PendingMessages { get; set; }
    public long ProcessedMessages { get; set; }
    public long DeadLetterMessages { get; set; }
    public int ConsumerGroupCount { get; set; }
}
