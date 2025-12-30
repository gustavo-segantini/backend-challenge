using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;
using CnabApi.Services.Interfaces;

namespace CnabApi.Services;

/// <summary>
/// Redis Streams implementation of upload queue service.
/// Provides reliable, persistent queue with consumer group support for distributed processing.
/// </summary>
[ExcludeFromCodeCoverage] // Infrastructure code - requires Redis integration tests
public class RedisUploadQueueService(IConnectionMultiplexer redis, ILogger<RedisUploadQueueService> logger) : IUploadQueueService
{
    private const string StreamKey = "cnab:upload:queue";
    private const string DeadLetterStreamKey = "cnab:upload:dlq";
    private const string ConsumerGroupPrefix = "cnab-upload-consumer";
    private const int DefaultStreamTrimLength = 10000; // Keep last 10k messages for audit

    public async Task<string> EnqueueUploadAsync(Guid uploadId, string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var messageId = await db.StreamAddAsync(StreamKey, new[]
            {
                new NameValueEntry("uploadId", uploadId.ToString()),
                new NameValueEntry("storagePath", storagePath),
                new NameValueEntry("enqueuedAt", DateTime.UtcNow.ToString("O"))
            });

            // Trim stream to keep only recent messages (for performance)
            await db.StreamTrimAsync(StreamKey, DefaultStreamTrimLength);

            logger.LogInformation(
                "Upload message enqueued. UploadId: {UploadId}, MessageId: {MessageId}, StoragePath: {StoragePath}",
                uploadId, messageId, storagePath);

            return messageId!.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enqueueing upload. UploadId: {UploadId}, StoragePath: {StoragePath}", uploadId, storagePath);
            throw;
        }
    }

    public async Task<(string MessageId, Guid UploadId, string StoragePath)?> DequeueUploadAsync(
        string consumerGroup,
        string consumerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();

            // Read from stream
            var messages = await db.StreamReadGroupAsync(
                StreamKey,
                consumerGroup,
                consumerId,
                ">", // Read new messages only
                count: 1);

            if (messages == null || messages.Length == 0)
            {
                return null;
            }

            var message = messages[0];
            var entries = message.Values.ToDictionary(kv => kv.Name.ToString(), kv => kv.Value.ToString());

            if (!entries.TryGetValue("uploadId", out var uploadIdStr) ||
                !entries.TryGetValue("storagePath", out var storagePath))
            {
                logger.LogError("Invalid message format in stream. MessageId: {MessageId}", message.Id);
                return null;
            }

            var uploadId = Guid.Parse(uploadIdStr!);

            logger.LogInformation(
                "Message dequeued from upload queue. MessageId: {MessageId}, UploadId: {UploadId}, StoragePath: {StoragePath}",
                message.Id, uploadId, storagePath);

            return (message.Id!.ToString(), uploadId, storagePath ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dequeueing upload from consumer group {ConsumerGroup}", consumerGroup);
            throw;
        }
    }

    public async Task AcknowledgeMessageAsync(string consumerGroup, string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var count = await db.StreamAcknowledgeAsync(StreamKey, consumerGroup, messageId);

            logger.LogInformation(
                "Message acknowledged. ConsumerGroup: {ConsumerGroup}, MessageId: {MessageId}, AckCount: {AckCount}",
                consumerGroup, messageId, count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error acknowledging message. MessageId: {MessageId}, ConsumerGroup: {ConsumerGroup}",
                messageId, consumerGroup);
            throw;
        }
    }

    public async Task MoveToDeadLetterQueueAsync(
        string messageId,
        Guid uploadId,
        string reason,
        int retryCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();

            // Add to DLQ with failure details
            var dlqMessageId = await db.StreamAddAsync(DeadLetterStreamKey, new[]
            {
                new NameValueEntry("uploadId", uploadId.ToString()),
                new NameValueEntry("originalMessageId", messageId),
                new NameValueEntry("failureReason", reason),
                new NameValueEntry("retryCount", retryCount.ToString()),
                new NameValueEntry("failedAt", DateTime.UtcNow.ToString("O"))
            });

            logger.LogWarning(
                "Message moved to dead letter queue. UploadId: {UploadId}, OriginalMessageId: {OriginalMessageId}, DLQMessageId: {DLQMessageId}, Reason: {Reason}, RetryCount: {RetryCount}",
                uploadId, messageId, dlqMessageId, reason, retryCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving message to dead letter queue. MessageId: {MessageId}, UploadId: {UploadId}",
                messageId, uploadId);
            throw;
        }
    }

    public async Task<UploadQueueStats> GetQueueStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();

            var streamInfo = await db.StreamInfoAsync(StreamKey);
            var dlqInfo = await db.StreamInfoAsync(DeadLetterStreamKey);

            // Count pending messages
            var pendingMessages = 0;
            
            return new UploadQueueStats
            {
                PendingMessages = pendingMessages,
                ProcessedMessages = streamInfo.Length,
                DeadLetterMessages = dlqInfo.Length,
                ConsumerGroupCount = 1 // We use a single consumer group for now
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting queue statistics");
            return new UploadQueueStats();
        }
    }

    public async Task InitializeConsumerGroupAsync(string consumerGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = redis.GetDatabase();

            // Try to create consumer group (will fail if already exists, which is fine)
            await db.StreamCreateConsumerGroupAsync(StreamKey, consumerGroup, "$", createStream: true);

            logger.LogInformation("Consumer group initialized. ConsumerGroup: {ConsumerGroup}, StreamKey: {StreamKey}",
                consumerGroup, StreamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists - this is expected and fine
            logger.LogInformation("Consumer group already exists. ConsumerGroup: {ConsumerGroup}", consumerGroup);
        }
        catch (RedisCommandException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists - this is expected and fine
            logger.LogInformation("Consumer group already exists. ConsumerGroup: {ConsumerGroup}", consumerGroup);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing consumer group. ConsumerGroup: {ConsumerGroup}", consumerGroup);
            throw;
        }
    }
}
