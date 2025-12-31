using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Interfaces;

namespace CnabApi.Services.UploadProcessing;

/// <summary>
/// Asynchronous upload processing strategy.
/// Enqueues files for background processing and returns immediately.
/// Used in production environments for better scalability and responsiveness.
/// </summary>
public class AsynchronousUploadProcessingStrategy(
    IUploadQueueService uploadQueueService,
    ILogger<AsynchronousUploadProcessingStrategy> logger) : IUploadProcessingStrategy
{
    public async Task<Result<UploadResult>> ProcessUploadAsync(
        string fileContent,
        FileUpload fileUploadRecord,
        string? storagePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messageId = await uploadQueueService.EnqueueUploadAsync(
                fileUploadRecord.Id,
                storagePath ?? string.Empty,
                cancellationToken);

            logger.LogInformation(
                "File enqueued for background processing. UploadId: {UploadId}, MessageId: {MessageId}",
                fileUploadRecord.Id, messageId);

            // Return 202 Accepted indicating the file has been queued for processing
            return Result<UploadResult>.Success(new UploadResult
            {
                TransactionCount = 0, // Processing not complete yet
                StatusCode = UploadStatusCode.Accepted, // 202 Accepted
                UploadId = fileUploadRecord.Id
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue file for background processing. UploadId: {UploadId}", fileUploadRecord.Id);

            return Result<UploadResult>.Failure(
                $"Failed to enqueue file for processing: {ex.Message}",
                new UploadResult
                {
                    TransactionCount = 0,
                    StatusCode = UploadStatusCode.InternalServerError, // 500
                    UploadId = fileUploadRecord.Id
                });
        }
    }
}

