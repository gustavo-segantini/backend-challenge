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
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
