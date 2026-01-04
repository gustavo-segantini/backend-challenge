using CnabApi.Common;
using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Models.Responses;
using CnabApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CnabApi.Services;

/// <summary>
/// Service for managing file upload operations and business logic.
/// Handles upload queries, validation, and resume operations.
/// </summary>
public class UploadManagementService(
    IFileUploadTrackingService fileUploadTrackingService,
    IUploadQueueService uploadQueueService,
    CnabDbContext dbContext,
    ILogger<UploadManagementService> logger) : IUploadManagementService
{
    private readonly IFileUploadTrackingService _fileUploadTrackingService = fileUploadTrackingService;
    private readonly IUploadQueueService _uploadQueueService = uploadQueueService;
    private readonly CnabDbContext _dbContext = dbContext;
    private readonly ILogger<UploadManagementService> _logger = logger;

    public async Task<PagedResponse<FileUploadResponse>> GetAllUploadsAsync(
        int page = 1,
        int pageSize = 50,
        FileUploadStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var (uploads, totalCount) = await _fileUploadTrackingService.GetAllUploadsAsync(
            page, pageSize, status, cancellationToken);

        var result = uploads.ToResponse().ToList();

        return new PagedResponse<FileUploadResponse>
        {
            Items = result,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    public async Task<Result<IncompleteUploadsResponse>> GetIncompleteUploadsAsync(
        int timeoutMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        var incompleteUploads = await _fileUploadTrackingService.FindIncompleteUploadsAsync(timeoutMinutes, cancellationToken);
        var result = incompleteUploads.ToResponse().ToList();

        return Result<IncompleteUploadsResponse>.Success(new IncompleteUploadsResponse {
            IncompleteUploads = result,
            Count = result.Count
        });
    }

    public async Task<Result<ResumeUploadResponse>> ResumeUploadAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _fileUploadTrackingService.GetUploadByIdAsync(uploadId, cancellationToken);

        if (upload == null)
        {
            return Result<ResumeUploadResponse>.Failure($"Upload with ID {uploadId} not found");
        }

        var isIncomplete = await _fileUploadTrackingService.IsUploadIncompleteAsync(uploadId, cancellationToken);
        if (!isIncomplete)
        {
            return Result<ResumeUploadResponse>.Failure("Upload is not incomplete or cannot be resumed");
        }

        if (string.IsNullOrEmpty(upload.StoragePath))
        {
            return Result<ResumeUploadResponse>.Failure("Upload does not have a storage path and cannot be resumed");
        }

        try
        {
            await _uploadQueueService.EnqueueUploadAsync(uploadId, upload.StoragePath, cancellationToken);

            _logger.LogInformation(
                "Upload re-enqueued for processing. UploadId: {UploadId}, WillResumeFromLine: {LastCheckpointLine}",
                uploadId, upload.LastCheckpointLine);

            return Result<ResumeUploadResponse>.Success(new ResumeUploadResponse
            {
                Message = "Upload re-enqueued for processing",
                UploadId = uploadId,
                WillResumeFromLine = upload.LastCheckpointLine,
                TotalLineCount = upload.TotalLineCount,
                ProcessedLineCount = upload.ProcessedLineCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming upload. UploadId: {UploadId}", uploadId);
            return Result<ResumeUploadResponse>.Failure($"Failed to resume upload: {ex.Message}");
        }
    }

    public async Task<Result<ResumeAllUploadsResponse>> ResumeAllIncompleteUploadsAsync(
        int timeoutMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        var incompleteUploads = await _fileUploadTrackingService.FindIncompleteUploadsAsync(timeoutMinutes, cancellationToken);

        var results = await Task.WhenAll(
            incompleteUploads.Select(upload => ProcessResumeUploadAsync(upload, cancellationToken)));

        var resumedUploads = results
            .Where(r => r.ResumedUpload != null)
            .Select(r => r.ResumedUpload!)
            .ToList();

        var errors = results
            .Where(r => r.Error != null)
            .Select(r => r.Error!)
            .ToList();

        return Result<ResumeAllUploadsResponse>.Success(new ResumeAllUploadsResponse
        {
            Message = $"Resumed {resumedUploads.Count} incomplete upload(s)",
            ResumedCount = resumedUploads.Count,
            ResumedUploads = resumedUploads,
            Errors = errors.Count > 0 ? errors : null
        });
    }

    private async Task<(ResumedUploadInfo? ResumedUpload, string? Error)> ProcessResumeUploadAsync(
        FileUpload upload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(upload.StoragePath))
        {
            return (null, $"Upload {upload.Id} does not have a storage path");
        }

        try
        {
            await _uploadQueueService.EnqueueUploadAsync(upload.Id, upload.StoragePath, cancellationToken);

            _logger.LogInformation(
                "Upload re-enqueued for processing. UploadId: {UploadId}, WillResumeFromLine: {LastCheckpointLine}",
                upload.Id, upload.LastCheckpointLine);

            return (new ResumedUploadInfo
            {
                UploadId = upload.Id,
                FileName = upload.FileName,
                WillResumeFromLine = upload.LastCheckpointLine,
                TotalLineCount = upload.TotalLineCount,
                ProcessedLineCount = upload.ProcessedLineCount
            }, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming upload. UploadId: {UploadId}", upload.Id);
            return (null, $"Error resuming upload {upload.Id}: {ex.Message}");
        }
    }

    public async Task<Result<FileUploadResponse>> GetUploadByIdAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _fileUploadTrackingService.GetUploadByIdAsync(uploadId, cancellationToken);

        if (upload == null)
        {
            return Result<FileUploadResponse>.Failure($"Upload with ID {uploadId} not found");
        }

        // Count actual transactions from database for this upload
        var actualProcessedCount = await _dbContext.Transactions
            .Where(t => t.FileUploadId == uploadId)
            .CountAsync(cancellationToken);

        // Count failed lines (lines that were attempted but failed)
        // This is stored in FileUploadLineHashes with a specific pattern or we use the FailedLineCount from the entity
        // For now, we'll use the FailedLineCount from the entity as it's tracked separately

        // Create response with accurate count
        var response = upload.ToResponse();
        
        // Override ProcessedLineCount with actual count from database
        response.ProcessedLineCount = actualProcessedCount;
        
        // Recalculate progress percentage based on actual count
        if (upload.TotalLineCount > 0)
        {
            var totalProcessed = actualProcessedCount + upload.FailedLineCount + upload.SkippedLineCount;
            response.ProgressPercentage = Math.Round((double)totalProcessed / upload.TotalLineCount * 100, 2);
        }
        else
        {
            response.ProgressPercentage = 0;
        }

        _logger.LogDebug(
            "Retrieved upload status. UploadId: {UploadId}, ActualProcessedCount: {ActualCount}, EntityProcessedCount: {EntityCount}",
            uploadId, actualProcessedCount, upload.ProcessedLineCount);

        return Result<FileUploadResponse>.Success(response);
    }
}

