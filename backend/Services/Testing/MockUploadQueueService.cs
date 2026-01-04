using System.Diagnostics.CodeAnalysis;
using CnabApi.Services.Interfaces;
using System.Collections.Concurrent;

namespace CnabApi.Services.Testing;

/// <summary>
/// Mock implementation of IUploadQueueService for testing environments.
/// Provides in-memory queue functionality without requiring Redis.
/// </summary>
[ExcludeFromCodeCoverage] // Testing infrastructure - not part of business logic
public class MockUploadQueueService(ILogger<MockUploadQueueService> logger) : IUploadQueueService
{
    private readonly ConcurrentQueue<(string MessageId, Guid UploadId, string StoragePath)> _queue = new();
    private readonly ILogger<MockUploadQueueService> _logger = logger;

    public Task InitializeConsumerGroupAsync(string consumerGroup, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MockUploadQueueService: Initializing consumer group '{ConsumerGroup}'", consumerGroup);
        return Task.CompletedTask;
    }

    public Task<string> EnqueueUploadAsync(Guid uploadId, string storagePath, CancellationToken cancellationToken)
    {
        var messageId = Guid.CreateVersion7().ToString();
        _queue.Enqueue((messageId, uploadId, storagePath));
        _logger.LogInformation("MockUploadQueueService: Enqueued message '{MessageId}' for upload '{UploadId}', StoragePath: '{StoragePath}'", 
            messageId, uploadId, storagePath);
        return Task.FromResult(messageId);
    }

    public Task<(string MessageId, Guid UploadId, string StoragePath)?> DequeueUploadAsync(
        string consumerGroup, 
        string consumerId, 
        CancellationToken cancellationToken)
    {
        _queue.TryDequeue(out var message);
        return Task.FromResult<(string MessageId, Guid UploadId, string StoragePath)?>(message != default ? message : null);
    }

    public Task AcknowledgeMessageAsync(string consumerGroup, string messageId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MockUploadQueueService: Acknowledged message '{MessageId}'", messageId);
        return Task.CompletedTask;
    }

    public Task MoveToDeadLetterQueueAsync(string messageId, Guid uploadId, string reason, int retryCount, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MockUploadQueueService: Moving message '{MessageId}' to DLQ for upload '{UploadId}': {Reason}", 
            messageId, uploadId, reason);
        return Task.CompletedTask;
    }

    public Task<UploadQueueStats> GetQueueStatsAsync(CancellationToken cancellationToken)
    {
        var stats = new UploadQueueStats
        {
            PendingMessages = _queue.Count,
            ProcessedMessages = 0,
            DeadLetterMessages = 0,
            ConsumerGroupCount = 1
        };
        return Task.FromResult(stats);
    }
}
