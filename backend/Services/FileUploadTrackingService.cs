using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Services.Interfaces;

namespace CnabApi.Services;

/// <summary>
/// Service for tracking file uploads and detecting duplicate uploads.
/// Uses SHA256 hashing for duplicate detection.
/// </summary>
public class FileUploadTrackingService(CnabDbContext db, ILogger<FileUploadTrackingService> logger) : IFileUploadTrackingService
{
    private readonly CnabDbContext _db = db;
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
            UploadedAt = DateTime.Now
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
            UploadedAt = DateTime.Now
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
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        // Ensure we're at the beginning of the stream
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        using var sha256 = SHA256.Create();
        var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));

        // Reset stream position for further reading
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        // Convert hash bytes to hexadecimal string
        return Convert.ToHexStringLower(hashBytes);
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
        var lineHashRecord = new FileUploadLineHash
        {
            Id = Guid.NewGuid(),
            FileUploadId = fileUploadId,
            LineHash = lineHash,
            LineContent = lineContent,
            ProcessedAt = DateTime.Now
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
        try
        {
            var changeCount = _db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);
            
            if (changeCount > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation(
                    "Committed {ChangeCount} line hash records to database",
                    changeCount);
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to commit line hash records. Database error: {Error}", ex.InnerException?.Message);
            throw;
        }
    }
}
