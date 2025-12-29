using CnabApi.Services.Interfaces;

namespace CnabApi.Services.LineProcessing;

/// <summary>
/// Manages checkpointing for resumable file processing.
/// Single Responsibility: Handle checkpoint logic only.
/// </summary>
public interface ICheckpointManager
{
    /// <summary>
    /// Checks if a checkpoint should be saved based on the number of processed lines.
    /// </summary>
    bool ShouldSaveCheckpoint(int totalProcessed, int checkpointInterval);

    /// <summary>
    /// Saves a checkpoint asynchronously (fire-and-forget).
    /// </summary>
    Task SaveCheckpointAsync(
        Guid fileUploadId,
        int lastCheckpointLine,
        int processedCount,
        int failedCount,
        int skippedCount,
        IFileUploadTrackingService fileUploadTrackingService,
        ILogger logger,
        CancellationToken cancellationToken);
}

