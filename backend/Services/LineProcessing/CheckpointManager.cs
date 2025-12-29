using CnabApi.Services.Interfaces;

namespace CnabApi.Services.LineProcessing;

/// <summary>
/// Manages checkpointing for resumable file processing.
/// Follows Single Responsibility Principle - only handles checkpoint logic.
/// </summary>
public class CheckpointManager : ICheckpointManager
{
    public bool ShouldSaveCheckpoint(int totalProcessed, int checkpointInterval)
    {
        return totalProcessed > 0 && totalProcessed % checkpointInterval == 0;
    }

    public async Task SaveCheckpointAsync(
        Guid fileUploadId,
        int lastCheckpointLine,
        int processedCount,
        int failedCount,
        int skippedCount,
        IFileUploadTrackingService fileUploadTrackingService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await fileUploadTrackingService.UpdateCheckpointAsync(
                fileUploadId,
                lastCheckpointLine,
                processedCount,
                failedCount,
                skippedCount,
                cancellationToken);

            logger.LogDebug(
                "Checkpoint saved. UploadId: {UploadId}, LastLine: {LastLine}",
                fileUploadId, lastCheckpointLine);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save checkpoint. UploadId: {UploadId}", fileUploadId);
            // Don't throw - checkpoints are best effort
        }
    }
}

