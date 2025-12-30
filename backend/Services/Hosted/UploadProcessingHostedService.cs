using CnabApi.Models;
using CnabApi.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Services.Hosted;

/// <summary>
/// Background service that processes file uploads from Redis queue.
/// Implements consumer group pattern for reliable, distributed processing with retry logic.
/// Downloads files from MinIO and processes line by line with checkpoint support.
/// </summary>
[ExcludeFromCodeCoverage]
public class UploadProcessingHostedService(
    IUploadQueueService queueService,
    IDistributedLockService lockService,
    IServiceProvider serviceProvider,
    IOptions<UploadProcessingOptions> options,
    ILogger<UploadProcessingHostedService> logger) : BackgroundService
{
    private const string ConsumerGroup = "cnab-upload-processors";
    private const int MaxRetries = 3;
    private const int BaseRetryDelayMs = 1000;
    private const double RetryBackoffMultiplier = 2.0;
    private const int ProcessingTimeoutSeconds = 3600; // 1 hour timeout per file
    private const int PollingIntervalMs = 1000;

    private readonly UploadProcessingOptions _options = options.Value;
    private readonly string _consumerId = $"worker-{Environment.MachineName}-{Process.GetCurrentProcess().Id}";
    private readonly string _instanceId = Guid.NewGuid().ToString()[..8];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Upload processing service started. ConsumerId: {ConsumerId}, InstanceId: {InstanceId}, ParallelWorkers: {Workers}, CheckpointInterval: {Interval}",
            _consumerId, _instanceId, _options.ParallelWorkers, _options.CheckpointInterval);

        try
        {
            await queueService.InitializeConsumerGroupAsync(ConsumerGroup, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var message = await queueService.DequeueUploadAsync(ConsumerGroup, _consumerId, stoppingToken);

                    if (message == null)
                    {
                        await Task.Delay(PollingIntervalMs, stoppingToken);
                        continue;
                    }

                    await ProcessUploadMessageAsync(message.Value, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Upload processing service cancellation requested");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error in upload processing loop. Will continue processing.");
                    await Task.Delay(PollingIntervalMs, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Critical error in upload processing service");
            throw;
        }
        finally
        {
            logger.LogInformation("Upload processing service stopped. InstanceId: {InstanceId}", _instanceId);
        }
    }

    /// <summary>
    /// Processes a single upload message with retry logic and distributed locking.
    /// </summary>
    private async Task ProcessUploadMessageAsync(
        (string MessageId, Guid UploadId, string StoragePath) message,
        CancellationToken stoppingToken)
    {
        var (messageId, uploadId, storagePath) = message;
        var lockKey = $"upload:processing:{uploadId}";

        logger.LogInformation(
            "Processing upload message. UploadId: {UploadId}, MessageId: {MessageId}, StoragePath: {StoragePath}",
            uploadId, messageId, storagePath);

        var (lockAcquired, _) = await lockService.ExecuteWithLockAsync<object?>(
            lockKey,
            async () =>
            {
                await ProcessUploadWithRetryAsync(uploadId, storagePath, messageId, stoppingToken);
                return null;
            },
            expirationSeconds: ProcessingTimeoutSeconds,
            cancellationToken: stoppingToken);

        if (!lockAcquired)
        {
            logger.LogWarning(
                "Could not acquire processing lock for upload. UploadId: {UploadId}. May be processing in another instance.",
                uploadId);
        }
    }

    /// <summary>
    /// Processes upload with automatic retry and exponential backoff.
    /// </summary>
    private async Task ProcessUploadWithRetryAsync(
        Guid uploadId,
        string storagePath,
        string messageId,
        CancellationToken stoppingToken)
    {
        int retryCount = 0;

        while (retryCount < MaxRetries)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var uploadService = scope.ServiceProvider.GetRequiredService<ICnabUploadService>();
                var fileUploadTrackingService = scope.ServiceProvider.GetRequiredService<IFileUploadTrackingService>();
                var objectStorageService = scope.ServiceProvider.GetRequiredService<IObjectStorageService>();

                // Update status to Processing
                await fileUploadTrackingService.UpdateProcessingStatusAsync(
                    uploadId,
                    FileUploadStatus.Processing,
                    retryCount,
                    stoppingToken);

                logger.LogInformation(
                    "Starting upload processing. UploadId: {UploadId}, Attempt: {Attempt}/{MaxRetries}, StoragePath: {StoragePath}",
                    uploadId, retryCount + 1, MaxRetries, storagePath);

                // Step 1: Download file from MinIO
                var fileBytes = await DownloadFileFromStorageAsync(objectStorageService, storagePath, stoppingToken);
                var fileContent = System.Text.Encoding.UTF8.GetString(fileBytes);

                logger.LogInformation(
                    "File downloaded from MinIO. UploadId: {UploadId}, Size: {Size} bytes",
                    uploadId, fileBytes.Length);

                // Step 2: Get existing upload record to check for checkpoint
                var uploadRecord = await fileUploadTrackingService.GetUploadByIdAsync(uploadId, stoppingToken);
                var startFromLine = uploadRecord?.LastCheckpointLine ?? 0;

                if (startFromLine > 0)
                {
                    logger.LogInformation(
                        "Resuming from checkpoint. UploadId: {UploadId}, StartLine: {StartLine}",
                        uploadId, startFromLine);
                }

                // Step 3: Process the file with parallel line processing
                var result = await uploadService.ProcessCnabUploadAsync(
                    fileContent,
                    uploadId,
                    startFromLine,
                    stoppingToken);

                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Unknown processing error");
                }

                // Step 4: Acknowledge the message in queue
                await queueService.AcknowledgeMessageAsync(ConsumerGroup, messageId, stoppingToken);

                logger.LogInformation(
                    "Upload processing completed successfully. UploadId: {UploadId}, TransactionCount: {TransactionCount}",
                    uploadId, result.Data);

                return; // Success
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Upload processing was cancelled. UploadId: {UploadId}", uploadId);
                throw;
            }
            catch (Exception ex)
            {
                retryCount++;
                var errorMessage = ex.Message;

                logger.LogWarning(ex,
                    "Upload processing failed (attempt {Attempt}/{MaxRetries}). UploadId: {UploadId}, Error: {Error}",
                    retryCount, MaxRetries, uploadId, errorMessage);

                if (retryCount >= MaxRetries)
                {
                    await HandleFinalFailureAsync(uploadId, messageId, errorMessage, retryCount, stoppingToken);
                    return;
                }

                var delayMs = (int)(BaseRetryDelayMs * Math.Pow(RetryBackoffMultiplier, retryCount - 1));
                logger.LogInformation("Retrying after {DelayMs}ms. UploadId: {UploadId}", delayMs, uploadId);

                await Task.Delay(delayMs, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Downloads file from MinIO with retry.
    /// </summary>
    private async Task<byte[]> DownloadFileFromStorageAsync(
        IObjectStorageService objectStorageService,
        string storagePath,
        CancellationToken stoppingToken)
    {
        const int maxDownloadRetries = 3;
        int attempt = 0;

        while (attempt < maxDownloadRetries)
        {
            try
            {
                attempt++;
                return await objectStorageService.DownloadFileAsync(storagePath, stoppingToken);
            }
            catch (Exception ex) when (attempt < maxDownloadRetries)
            {
                logger.LogWarning(ex,
                    "Failed to download file from MinIO (attempt {Attempt}/{Max}). StoragePath: {Path}",
                    attempt, maxDownloadRetries, storagePath);

                await Task.Delay(500 * attempt, stoppingToken);
            }
        }

        throw new InvalidOperationException($"Failed to download file from storage after {maxDownloadRetries} attempts: {storagePath}");
    }

    /// <summary>
    /// Handles final failure after all retries exhausted.
    /// </summary>
    private async Task HandleFinalFailureAsync(
        Guid uploadId,
        string messageId,
        string errorMessage,
        int retryCount,
        CancellationToken stoppingToken)
    {
        try
        {
            logger.LogError(
                "Upload processing failed after {RetryCount} retries. UploadId: {UploadId}, Error: {Error}",
                retryCount, uploadId, errorMessage);

            await queueService.MoveToDeadLetterQueueAsync(messageId, uploadId, errorMessage, retryCount, stoppingToken);

            using var scope = serviceProvider.CreateScope();
            var fileUploadTrackingService = scope.ServiceProvider.GetRequiredService<IFileUploadTrackingService>();

            await fileUploadTrackingService.UpdateProcessingFailureAsync(
                uploadId,
                errorMessage,
                retryCount,
                stoppingToken);

            logger.LogError("Upload moved to dead letter queue. UploadId: {UploadId}", uploadId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling final failure for upload. UploadId: {UploadId}", uploadId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Upload processing service stopping gracefully. InstanceId: {InstanceId}", _instanceId);
        await base.StopAsync(cancellationToken);
    }
}
