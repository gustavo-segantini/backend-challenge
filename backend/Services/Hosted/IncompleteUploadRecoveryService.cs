using CnabApi.Models;
using CnabApi.Services.Interfaces;
using CnabApi.Utilities;
using Microsoft.Extensions.Options;
using Serilog.Context;
using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Services.Hosted;

/// <summary>
/// Background service that automatically detects and recovers incomplete uploads.
/// Periodically checks for uploads stuck in Processing status and re-enqueues them.
/// This ensures automatic recovery after application restarts or interruptions.
/// </summary>
[ExcludeFromCodeCoverage]
public class IncompleteUploadRecoveryService(
    IFileUploadTrackingService fileUploadTrackingService,
    IUploadQueueService uploadQueueService,
    IDistributedLockService lockService,
    IOptions<UploadProcessingOptions> options,
    ILogger<IncompleteUploadRecoveryService> logger) : BackgroundService
{
    private readonly UploadProcessingOptions _options = options.Value;
    
    private int RecoveryCheckIntervalMinutes => _options.RecoveryCheckIntervalMinutes > 0 
        ? _options.RecoveryCheckIntervalMinutes 
        : 5; // Default: 5 minutes
    
    private int StuckUploadTimeoutMinutes => _options.StuckUploadTimeoutMinutes > 0 
        ? _options.StuckUploadTimeoutMinutes 
        : 30; // Default: 30 minutes

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var correlationId = CorrelationIdHelper.GetOrCreateCorrelationId();
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            logger.LogInformation(
                "Incomplete upload recovery service started. CheckInterval: {Interval} minutes, StuckTimeout: {Timeout} minutes",
                RecoveryCheckIntervalMinutes, StuckUploadTimeoutMinutes);

            // Wait a bit before first check to allow application to fully start
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RecoverIncompleteUploadsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Incomplete upload recovery service cancellation requested");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in incomplete upload recovery service. Will retry on next interval.");
                }

                // Wait for the configured interval before next check
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(RecoveryCheckIntervalMinutes), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            logger.LogInformation("Incomplete upload recovery service stopped");
        }
    }

    /// <summary>
    /// Finds and recovers incomplete uploads by re-enqueuing them for processing.
    /// </summary>
    private async Task RecoverIncompleteUploadsAsync(CancellationToken cancellationToken)
    {
        // Ensure CorrelationId is set for this operation
        var correlationId = CorrelationIdHelper.GetOrCreateCorrelationId();
        CorrelationIdHelper.SetCorrelationId(correlationId);
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            logger.LogDebug("Checking for incomplete uploads...");

            var incompleteUploads = await fileUploadTrackingService.FindIncompleteUploadsAsync(
                StuckUploadTimeoutMinutes, 
                cancellationToken);

            if (incompleteUploads.Count == 0)
            {
                logger.LogDebug("No incomplete uploads found");
                return;
            }

            logger.LogInformation(
                "Found {Count} incomplete upload(s) that need recovery",
                incompleteUploads.Count);

            var recoveredCount = 0;
            var errorCount = 0;

            foreach (var upload in incompleteUploads)
            {
                try
                {
                    // Skip if upload doesn't have storage path (cannot be recovered)
                    if (string.IsNullOrEmpty(upload.StoragePath))
                    {
                        logger.LogWarning(
                            "Cannot recover upload {UploadId}: missing storage path",
                            upload.Id);
                        errorCount++;
                        continue;
                    }

                    // Check if upload is currently being processed by another worker
                    var lockKey = $"upload:processing:{upload.Id}";
                    var isLocked = await lockService.LockExistsAsync(lockKey, cancellationToken);

                    if (isLocked)
                    {
                        logger.LogInformation(
                            "Skipping upload {UploadId}: currently being processed by another worker (lock exists)",
                            upload.Id);
                        continue;
                    }

                    // Double-check: verify checkpoint hasn't been updated recently
                    // This handles race conditions where checkpoint was just saved
                    if (upload.LastCheckpointAt.HasValue)
                    {
                        var checkpointAge = DateTime.UtcNow - upload.LastCheckpointAt.Value;
                        var checkpointTimeoutMinutes = StuckUploadTimeoutMinutes / 2; // More lenient for checkpoint check
                        
                        if (checkpointAge.TotalMinutes < checkpointTimeoutMinutes)
                        {
                            logger.LogInformation(
                                "Skipping upload {UploadId}: checkpoint was updated recently ({AgeMinutes:F1} minutes ago)",
                                upload.Id, checkpointAge.TotalMinutes);
                            continue;
                        }
                    }

                    // Re-enqueue the upload for processing
                    await uploadQueueService.EnqueueUploadAsync(upload.Id, upload.StoragePath, cancellationToken);

                    recoveredCount++;

                    logger.LogInformation(
                        "Recovered incomplete upload. UploadId: {UploadId}, FileName: {FileName}, " +
                        "WillResumeFromLine: {LastCheckpointLine}, Progress: {Processed}/{Total} lines",
                        upload.Id,
                        upload.FileName,
                        upload.LastCheckpointLine,
                        upload.ProcessedLineCount,
                        upload.TotalLineCount);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    logger.LogError(ex,
                        "Failed to recover upload {UploadId}: {Error}",
                        upload.Id, ex.Message);
                }
            }

            if (recoveredCount > 0)
            {
                logger.LogInformation(
                    "Recovery completed. Recovered: {Recovered}, Errors: {Errors}",
                    recoveredCount, errorCount);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Incomplete upload recovery service stopping gracefully");
        await base.StopAsync(cancellationToken);
    }
}

