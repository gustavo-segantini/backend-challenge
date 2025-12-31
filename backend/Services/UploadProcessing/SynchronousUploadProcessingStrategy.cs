using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Interfaces;

namespace CnabApi.Services.UploadProcessing;

/// <summary>
/// Synchronous upload processing strategy.
/// Processes files immediately and returns the final result.
/// Used primarily in test environments where immediate feedback is required.
/// </summary>
public class SynchronousUploadProcessingStrategy(
    ICnabUploadService cnabUploadService,
    IFileUploadTrackingService fileUploadTrackingService,
    ILogger<SynchronousUploadProcessingStrategy> logger) : IUploadProcessingStrategy
{
    public async Task<Result<UploadResult>> ProcessUploadAsync(
        string fileContent,
        FileUpload fileUploadRecord,
        string? storagePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Processing file synchronously. UploadId: {UploadId}",
                fileUploadRecord.Id);

            // Update status to Processing
            await fileUploadTrackingService.UpdateProcessingStatusAsync(
                fileUploadRecord.Id,
                FileUploadStatus.Processing,
                0,
                cancellationToken);

            // Process the file synchronously
            var processResult = await cnabUploadService.ProcessCnabUploadAsync(
                fileContent,
                fileUploadRecord.Id,
                startFromLine: 0,
                cancellationToken);

            if (processResult.IsSuccess)
            {
                await fileUploadTrackingService.UpdateProcessingSuccessAsync(
                    fileUploadRecord.Id,
                    processResult.Data,
                    storagePath,
                    cancellationToken);

                logger.LogInformation(
                    "File processed successfully. UploadId: {UploadId}, TransactionCount: {Count}",
                    fileUploadRecord.Id, processResult.Data);

                return Result<UploadResult>.Success(new UploadResult
                {
                    TransactionCount = processResult.Data,
                    StatusCode = UploadStatusCode.Success, // 200 OK
                    UploadId = fileUploadRecord.Id
                });
            }
            else
            {
                await fileUploadTrackingService.UpdateProcessingFailureAsync(
                    fileUploadRecord.Id,
                    processResult.ErrorMessage ?? "Processing failed",
                    0,
                    cancellationToken);

                return Result<UploadResult>.Failure(
                    processResult.ErrorMessage ?? "Failed to process file",
                    new UploadResult
                    {
                        TransactionCount = 0,
                        StatusCode = UploadStatusCode.UnprocessableEntity, // 422
                        UploadId = fileUploadRecord.Id
                    });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process file synchronously. UploadId: {UploadId}", fileUploadRecord.Id);

            await fileUploadTrackingService.UpdateProcessingFailureAsync(
                fileUploadRecord.Id,
                $"Processing failed: {ex.Message}",
                0,
                cancellationToken);

            return Result<UploadResult>.Failure(
                $"Failed to process file: {ex.Message}",
                new UploadResult
                {
                    TransactionCount = 0,
                    StatusCode = UploadStatusCode.InternalServerError, // 500
                    UploadId = fileUploadRecord.Id
                });
        }
    }
}

