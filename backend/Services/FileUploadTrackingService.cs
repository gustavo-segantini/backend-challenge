using Microsoft.EntityFrameworkCore;
using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Services.Interfaces;

namespace CnabApi.Services;

/// <summary>
/// Service for tracking file uploads and detecting duplicate uploads.
/// Uses SHA256 hashing for duplicate detection via IHashService (DRY principle).
/// </summary>
public class FileUploadTrackingService(
    CnabDbContext db,
    IHashService hashService,
    ILogger<FileUploadTrackingService> logger) : IFileUploadTrackingService
{
    private readonly CnabDbContext _db = db;
    private readonly IHashService _hashService = hashService;
    private readonly ILogger<FileUploadTrackingService> _logger = logger;

    public async Task<(bool IsUnique, FileUpload? ExistingUpload)> IsFileUniqueAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileHash))
            return (true, null);

        var existingUpload = await _db.FileUploads
            .FirstOrDefaultAsync(fu => fu.FileHash == fileHash, cancellationToken);

        if (existingUpload != null)
        {
            _logger.LogInformation(
                "Duplicate file detected. Hash: {FileHash}, Previous upload: {PreviousUpload} ({ProcessedLines} lines)",
                fileHash,
                existingUpload.FileName,
                existingUpload.ProcessedLineCount);

            return (false, existingUpload);
        }

        return (true, null);
    }

    public async Task<FileUpload> RecordSuccessfulUploadAsync(
        string fileName,
        string fileHash,
        long fileSize,
        int processedLineCount,
        string? storagePath = null,
        CancellationToken cancellationToken = default)
    {
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            FileHash = fileHash,
            FileSize = fileSize,
            ProcessedLineCount = processedLineCount,
            Status = FileUploadStatus.Success,
            StoragePath = storagePath,
            UploadedAt = DateTime.UtcNow
        };

        _db.FileUploads.Add(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "File upload recorded successfully. Id: {UploadId}, File: {FileName}, Hash: {FileHash}, Lines: {LineCount}",
            fileUpload.Id,
            fileName,
            fileHash,
            processedLineCount);

        return fileUpload;
    }

    public async Task<FileUpload> RecordFailedUploadAsync(
        string fileName,
        string fileHash,
        long fileSize,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            FileHash = fileHash,
            FileSize = fileSize,
            ProcessedLineCount = 0,
            Status = FileUploadStatus.Failed,
            ErrorMessage = errorMessage,
            UploadedAt = DateTime.UtcNow
        };

        _db.FileUploads.Add(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "File upload recorded as failed. Id: {UploadId}, File: {FileName}, Error: {ErrorMessage}",
            fileUpload.Id,
            fileName,
            errorMessage);

        return fileUpload;
    }

    public async Task<string> CalculateFileHashAsync(Stream stream)
    {
        return await _hashService.ComputeStreamHashAsync(stream);
    }

    public async Task<bool> IsLineUniqueAsync(string lineHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lineHash))
            return true;

        var existingLineHash = await _db.FileUploadLineHashes
            .FirstOrDefaultAsync(lh => lh.LineHash == lineHash, cancellationToken);

        if (existingLineHash != null)
        {
            _logger.LogWarning(
                "Duplicate line detected. Hash: {LineHash}, Previous upload: {FileUploadId}",
                lineHash,
                existingLineHash.FileUploadId);

            return false;
        }

        return true;
    }

    public async Task RecordLineHashAsync(Guid fileUploadId, string lineHash, string lineContent, CancellationToken cancellationToken = default)
    {        
        // First check pending entries in the ChangeTracker to avoid duplicate adds within the same DbContext
        var pendingExists = _db.ChangeTracker.Entries<FileUploadLineHash>()
            .Any(e => e.State == EntityState.Added && ((FileUploadLineHash)e.Entity).LineHash == lineHash);

        if (pendingExists)
        {
            _logger.LogDebug(
                "Line hash already exists. FileUploadId: {FileUploadId}, LineHash: {LineHash}",
                fileUploadId,
                lineHash);
            return;
        }

        // If not pending, check persisted DB state as well
        var exists = await _db.FileUploadLineHashes
            .AsNoTracking()
            .AnyAsync(lh => lh.LineHash == lineHash, cancellationToken);

        if (exists)
        {
            _logger.LogDebug(
                "Line hash already exists in database. FileUploadId: {FileUploadId}, LineHash: {LineHash}",
                fileUploadId,
                lineHash);
            return;
        }

        var lineHashRecord = new FileUploadLineHash
        {
            Id = Guid.NewGuid(),
            FileUploadId = fileUploadId,
            LineHash = lineHash,
            LineContent = lineContent,
            ProcessedAt = DateTime.UtcNow
        };

        _db.FileUploadLineHashes.Add(lineHashRecord);
        
        _logger.LogDebug(
            "Line hash added to change tracker. FileUploadId: {FileUploadId}, LineHash: {LineHash}",
            fileUploadId,
            lineHash);
    }

    /// <summary>
    /// Commits all pending line hash records to the database in a single operation.
    /// Should be called after all line hashes have been added via RecordLineHashAsync.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous save operation</returns>
    public async Task CommitLineHashesAsync(CancellationToken cancellationToken = default)
    {
        // Commit pending FileUploadLineHash entries using an idempotent DB insert
        // to avoid unique constraint violations caused by concurrent processing.
        var pendingEntries = _db.ChangeTracker.Entries<FileUploadLineHash>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        if (!pendingEntries.Any())
            return;

        try
        {
            var inserted = 0;

            // Use parameterized interpolated SQL to leverage ON CONFLICT DO NOTHING
            foreach (var entry in pendingEntries)
            {
                // ExecuteSqlInterpolatedAsync will parameterize the values
                var result = await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"FileUploadLineHashes\" (\"Id\", \"FileUploadId\", \"LineContent\", \"LineHash\", \"ProcessedAt\") VALUES ({entry.Id}, {entry.FileUploadId}, {entry.LineContent}, {entry.LineHash}, {entry.ProcessedAt}) ON CONFLICT (\"LineHash\") DO NOTHING", cancellationToken);
                // result is number of rows affected for this statement (0 or 1)
                if (result > 0) inserted += result;

                // Detach the tracked entity so SaveChanges won't try to insert it again
                var tracked = _db.ChangeTracker.Entries<FileUploadLineHash>()
                    .FirstOrDefault(e => e.State == EntityState.Added && e.Entity.Id == entry.Id);
                if (tracked != null)
                    tracked.State = EntityState.Detached;
            }

            _logger.LogInformation("Committed {Inserted} line hash records to database (ON CONFLICT DO NOTHING)", inserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit line hash records. Database error: {Error}", ex.Message);
            throw;
        }
    }

    public async Task<FileUpload> RecordPendingUploadAsync(
        string fileName,
        string fileHash,
        long fileSize,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            FileHash = fileHash,
            FileSize = fileSize,
            ProcessedLineCount = 0,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow,
            RetryCount = 0,
            StoragePath = storagePath
        };

        _db.FileUploads.Add(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "File upload recorded as pending. Id: {UploadId}, File: {FileName}, Hash: {FileHash}, StoragePath: {StoragePath}",
            fileUpload.Id,
            fileName,
            fileHash,
            storagePath);

        return fileUpload;
    }

    public async Task UpdateProcessingStatusAsync(
        Guid uploadId,
        FileUploadStatus status,
        int retryCount,
        CancellationToken cancellationToken = default)
    {
        var fileUpload = await _db.FileUploads.FindAsync(new object[] { uploadId }, cancellationToken: cancellationToken);
        
        if (fileUpload == null)
        {
            _logger.LogWarning("FileUpload not found for status update. UploadId: {UploadId}", uploadId);
            return;
        }

        fileUpload.Status = status;
        fileUpload.RetryCount = retryCount;
        
        if (status == FileUploadStatus.Processing && fileUpload.ProcessingStartedAt == null)
        {
            fileUpload.ProcessingStartedAt = DateTime.UtcNow;
        }

        _db.FileUploads.Update(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "File upload status updated. UploadId: {UploadId}, Status: {Status}, RetryCount: {RetryCount}",
            uploadId, status, retryCount);
    }

    public async Task UpdateProcessingSuccessAsync(
        Guid uploadId,
        int processedLineCount,
        string? storagePath,
        CancellationToken cancellationToken = default)
    {
        var fileUpload = await _db.FileUploads.FindAsync(new object[] { uploadId }, cancellationToken: cancellationToken);
        
        if (fileUpload == null)
        {
            _logger.LogWarning("FileUpload not found for success update. UploadId: {UploadId}", uploadId);
            return;
        }

        fileUpload.Status = FileUploadStatus.Success;
        fileUpload.ProcessedLineCount = processedLineCount;
        fileUpload.StoragePath = storagePath;
        fileUpload.ProcessingCompletedAt = DateTime.UtcNow;
        fileUpload.RetryCount = 0; // Reset retry count on success
        fileUpload.ErrorMessage = null; // Clear error message

        _db.FileUploads.Update(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "File upload completed successfully. UploadId: {UploadId}, ProcessedLines: {ProcessedLineCount}, StoragePath: {StoragePath}",
            uploadId, processedLineCount, storagePath);
    }

    public async Task UpdateProcessingFailureAsync(
        Guid uploadId,
        string errorMessage,
        int retryCount,
        CancellationToken cancellationToken = default)
    {
        var fileUpload = await _db.FileUploads.FindAsync(new object[] { uploadId }, cancellationToken: cancellationToken);
        
        if (fileUpload == null)
        {
            _logger.LogWarning("FileUpload not found for failure update. UploadId: {UploadId}", uploadId);
            return;
        }

        fileUpload.Status = FileUploadStatus.Failed;
        fileUpload.ErrorMessage = errorMessage;
        fileUpload.RetryCount = retryCount;
        fileUpload.ProcessingCompletedAt = DateTime.UtcNow;

        _db.FileUploads.Update(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "File upload processing failed. UploadId: {UploadId}, Error: {ErrorMessage}, RetryCount: {RetryCount}",
            uploadId, errorMessage, retryCount);
    }

    public async Task<FileUpload?> GetUploadByIdAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        return await _db.FileUploads
            .Include(fu => fu.LineHashes)
            .FirstOrDefaultAsync(fu => fu.Id == uploadId, cancellationToken);
    }

    public async Task UpdateCheckpointAsync(
        Guid uploadId,
        int lastCheckpointLine,
        int processedCount,
        int failedCount,
        int skippedCount,
        CancellationToken cancellationToken = default)
    {
        var fileUpload = await _db.FileUploads.FindAsync(new object[] { uploadId }, cancellationToken: cancellationToken);
        
        if (fileUpload == null)
        {
            _logger.LogWarning("FileUpload not found for checkpoint update. UploadId: {UploadId}", uploadId);
            return;
        }

        // Checkpoint receives total counts (not incremental), so use them directly
        // The calling code is responsible for calculating totals correctly
        fileUpload.LastCheckpointLine = lastCheckpointLine;
        fileUpload.LastCheckpointAt = DateTime.UtcNow;
        fileUpload.ProcessedLineCount = processedCount;
        fileUpload.FailedLineCount = failedCount;
        fileUpload.SkippedLineCount = skippedCount;

        _db.FileUploads.Update(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Checkpoint updated. UploadId: {UploadId}, LastLine: {LastLine}, Processed: {Processed}, Failed: {Failed}, Skipped: {Skipped}",
            uploadId, lastCheckpointLine, processedCount, failedCount, skippedCount);
    }

    public async Task SetTotalLineCountAsync(Guid uploadId, int totalLineCount, CancellationToken cancellationToken = default)
    {
        var fileUpload = await _db.FileUploads.FindAsync(new object[] { uploadId }, cancellationToken: cancellationToken);
        
        if (fileUpload == null)
        {
            _logger.LogWarning("FileUpload not found for total line count update. UploadId: {UploadId}", uploadId);
            return;
        }

        fileUpload.TotalLineCount = totalLineCount;

        _db.FileUploads.Update(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Total line count set. UploadId: {UploadId}, TotalLineCount: {TotalLineCount}",
            uploadId, totalLineCount);
    }

    public async Task UpdateProcessingResultAsync(
        Guid uploadId,
        int processedCount,
        int failedCount,
        int skippedCount,
        CancellationToken cancellationToken = default)
    {
        var fileUpload = await _db.FileUploads.FindAsync(new object[] { uploadId }, cancellationToken: cancellationToken);
        
        if (fileUpload == null)
        {
            _logger.LogWarning("FileUpload not found for result update. UploadId: {UploadId}", uploadId);
            return;
        }

        fileUpload.ProcessedLineCount = processedCount;
        fileUpload.FailedLineCount = failedCount;
        fileUpload.SkippedLineCount = skippedCount;
        fileUpload.ProcessingCompletedAt = DateTime.UtcNow;

        // Calculate total processed lines
        var totalProcessed = processedCount + failedCount + skippedCount;
        // Use >= to handle edge cases where totalProcessed might slightly exceed TotalLineCount due to rounding
        // Also handle case where TotalLineCount might be 0 (shouldn't happen, but defensive)
        var allLinesProcessed = fileUpload.TotalLineCount > 0 && totalProcessed >= fileUpload.TotalLineCount;

        _logger.LogDebug(
            "Updating processing result. UploadId: {UploadId}, TotalLineCount: {Total}, TotalProcessed: {TotalProcessed} (Processed: {Processed}, Failed: {Failed}, Skipped: {Skipped}), AllLinesProcessed: {AllProcessed}",
            uploadId, fileUpload.TotalLineCount, totalProcessed, processedCount, failedCount, skippedCount, allLinesProcessed);

        // Determine final status
        if (allLinesProcessed)
        {
            // All lines have been processed - determine final status
            if (failedCount > 0)
            {
                fileUpload.Status = FileUploadStatus.PartiallyCompleted;
            }
            else
            {
                fileUpload.Status = FileUploadStatus.Success;
            }
            
            // Update last checkpoint to the final line if all are processed
            if (fileUpload.TotalLineCount > 0)
            {
                fileUpload.LastCheckpointLine = fileUpload.TotalLineCount - 1; // 0-based index
                fileUpload.LastCheckpointAt = DateTime.UtcNow;
            }
            
            _logger.LogInformation(
                "All lines processed. UploadId: {UploadId}, Status: {Status}, TotalLineCount: {Total}, TotalProcessed: {TotalProcessed}",
                uploadId, fileUpload.Status, fileUpload.TotalLineCount, totalProcessed);
        }
        else
        {
            // Not all lines processed yet - keep Processing status if it was Processing
            // Only change status if it's not already Processing or Pending
            if (fileUpload.Status != FileUploadStatus.Processing && fileUpload.Status != FileUploadStatus.Pending)
            {
                // If we have some failures but not all lines processed, mark as partially completed
                if (failedCount > 0)
                {
                    fileUpload.Status = FileUploadStatus.PartiallyCompleted;
                }
            }
            
            _logger.LogDebug(
                "Not all lines processed yet. UploadId: {UploadId}, Status: {Status}, TotalLineCount: {Total}, TotalProcessed: {TotalProcessed}",
                uploadId, fileUpload.Status, fileUpload.TotalLineCount, totalProcessed);
        }

        _db.FileUploads.Update(fileUpload);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Processing result updated. UploadId: {UploadId}, Status: {Status}, Processed: {Processed}, Failed: {Failed}, Skipped: {Skipped}, Total: {Total}, AllProcessed: {AllProcessed}",
            uploadId, fileUpload.Status, processedCount, failedCount, skippedCount, totalProcessed, allLinesProcessed);
    }

    public async Task<List<FileUpload>> FindIncompleteUploadsAsync(int timeoutMinutes = 30, CancellationToken cancellationToken = default)
    {
        var timeoutThreshold = DateTime.UtcNow.AddMinutes(-timeoutMinutes);

        // Find uploads that are:
        // 1. In Processing status
        // 2. Started processing more than timeoutMinutes ago
        // 3. AND either:
        //    - Have no checkpoint (never saved progress), OR
        //    - Last checkpoint was more than timeoutMinutes ago (no recent progress)
        var incompleteUploads = await _db.FileUploads
            .Where(u => u.Status == FileUploadStatus.Processing &&
                       u.ProcessingStartedAt.HasValue &&
                       u.ProcessingStartedAt.Value < timeoutThreshold &&
                       (u.LastCheckpointAt == null || u.LastCheckpointAt.Value < timeoutThreshold))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Found {Count} incomplete uploads (stuck in Processing status for more than {TimeoutMinutes} minutes with no recent checkpoint)",
            incompleteUploads.Count, timeoutMinutes);

        return incompleteUploads;
    }

    public async Task<bool> IsUploadIncompleteAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        var fileUpload = await _db.FileUploads.FindAsync(new object[] { uploadId }, cancellationToken: cancellationToken);

        if (fileUpload == null)
        {
            return false;
        }

        // Upload is incomplete if:
        // 1. Status is Processing or Pending
        // 2. Or if TotalLineCount > 0 and ProcessedLineCount + FailedLineCount + SkippedLineCount < TotalLineCount
        if (fileUpload.Status == FileUploadStatus.Processing || fileUpload.Status == FileUploadStatus.Pending)
        {
            return true;
        }

        if (fileUpload.TotalLineCount > 0)
        {
            var totalProcessed = fileUpload.ProcessedLineCount + fileUpload.FailedLineCount + fileUpload.SkippedLineCount;
            return totalProcessed < fileUpload.TotalLineCount;
        }

        return false;
    }

    public async Task<(List<FileUpload> Uploads, int TotalCount)> GetAllUploadsAsync(
        int page = 1,
        int pageSize = 50,
        FileUploadStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.FileUploads.AsQueryable();

        // Apply status filter if provided
        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination and ordering (most recent first)
        var uploads = await query
            .OrderByDescending(u => u.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (uploads, totalCount);
    }
}
