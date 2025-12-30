using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for FileUploadTrackingService.
/// Tests file upload tracking, duplicate detection, and status management.
/// </summary>
public class FileUploadTrackingServiceTests : IDisposable
{
    private readonly CnabDbContext _context;
    private readonly Mock<IHashService> _hashServiceMock;
    private readonly Mock<ILogger<FileUploadTrackingService>> _loggerMock;
    private readonly FileUploadTrackingService _service;

    public FileUploadTrackingServiceTests()
    {
        var options = new DbContextOptionsBuilder<CnabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CnabDbContext(options);
        _hashServiceMock = new Mock<IHashService>();
        _loggerMock = new Mock<ILogger<FileUploadTrackingService>>();

        _service = new FileUploadTrackingService(_context, _hashServiceMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region IsFileUniqueAsync Tests

    [Fact]
    public async Task IsFileUniqueAsync_WithUniqueFile_ShouldReturnTrue()
    {
        // Arrange
        const string fileHash = "unique-hash-123";

        // Act
        var (isUnique, existingUpload) = await _service.IsFileUniqueAsync(fileHash);

        // Assert
        isUnique.Should().BeTrue();
        existingUpload.Should().BeNull();
    }

    [Fact]
    public async Task IsFileUniqueAsync_WithDuplicateFile_ShouldReturnFalse()
    {
        // Arrange
        const string fileHash = "duplicate-hash-123";
        var existingUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "existing.txt",
            FileHash = fileHash,
            FileSize = 1000,
            ProcessedLineCount = 10,
            Status = FileUploadStatus.Success
        };

        _context.FileUploads.Add(existingUpload);
        await _context.SaveChangesAsync();

        // Act
        var (isUnique, foundUpload) = await _service.IsFileUniqueAsync(fileHash);

        // Assert
        isUnique.Should().BeFalse();
        foundUpload.Should().NotBeNull();
        foundUpload!.FileName.Should().Be("existing.txt");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsFileUniqueAsync_WithNullOrWhiteSpaceHash_ShouldReturnTrue(string? fileHash)
    {
        // Act
        var (isUnique, existingUpload) = await _service.IsFileUniqueAsync(fileHash!);

        // Assert
        isUnique.Should().BeTrue();
        existingUpload.Should().BeNull();
    }

    #endregion

    #region RecordSuccessfulUploadAsync Tests

    [Fact]
    public async Task RecordSuccessfulUploadAsync_WithValidData_ShouldCreateFileUpload()
    {
        // Arrange
        const string fileName = "test.txt";
        const string fileHash = "hash-123";
        const long fileSize = 5000;
        const int processedLineCount = 25;

        // Act
        var result = await _service.RecordSuccessfulUploadAsync(
            fileName, fileHash, fileSize, processedLineCount);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(fileName);
        result.FileHash.Should().Be(fileHash);
        result.FileSize.Should().Be(fileSize);
        result.ProcessedLineCount.Should().Be(processedLineCount);
        result.Status.Should().Be(FileUploadStatus.Success);

        var saved = await _context.FileUploads.FindAsync(result.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordSuccessfulUploadAsync_WithStoragePath_ShouldSaveStoragePath()
    {
        // Arrange
        const string storagePath = "minio/bucket/file.txt";

        // Act
        var result = await _service.RecordSuccessfulUploadAsync(
            "test.txt", "hash-123", 1000, 10, storagePath);

        // Assert
        result.StoragePath.Should().Be(storagePath);
    }

    #endregion

    #region RecordFailedUploadAsync Tests

    [Fact]
    public async Task RecordFailedUploadAsync_WithValidData_ShouldCreateFailedFileUpload()
    {
        // Arrange
        const string fileName = "test.txt";
        const string fileHash = "hash-123";
        const long fileSize = 5000;
        const string errorMessage = "Invalid format";

        // Act
        var result = await _service.RecordFailedUploadAsync(
            fileName, fileHash, fileSize, errorMessage);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(fileName);
        result.FileHash.Should().Be(fileHash);
        result.FileSize.Should().Be(fileSize);
        result.Status.Should().Be(FileUploadStatus.Failed);
        result.ErrorMessage.Should().Be(errorMessage);
        result.ProcessedLineCount.Should().Be(0);
    }

    #endregion

    #region RecordPendingUploadAsync Tests

    [Fact]
    public async Task RecordPendingUploadAsync_WithValidData_ShouldCreatePendingFileUpload()
    {
        // Arrange
        const string fileName = "test.txt";
        const string fileHash = "hash-123";
        const long fileSize = 5000;
        const string storagePath = "minio/bucket/file.txt";

        // Act
        var result = await _service.RecordPendingUploadAsync(
            fileName, fileHash, fileSize, storagePath);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be(fileName);
        result.FileHash.Should().Be(fileHash);
        result.FileSize.Should().Be(fileSize);
        result.Status.Should().Be(FileUploadStatus.Pending);
        result.StoragePath.Should().Be(storagePath);
        result.ProcessedLineCount.Should().Be(0);
        result.RetryCount.Should().Be(0);
    }

    #endregion

    #region CalculateFileHashAsync Tests

    [Fact]
    public async Task CalculateFileHashAsync_WithValidStream_ShouldReturnHash()
    {
        // Arrange
        var content = "test content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        const string expectedHash = "expected-hash-123";

        _hashServiceMock
            .Setup(x => x.ComputeStreamHashAsync(stream, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedHash);

        // Act
        var result = await _service.CalculateFileHashAsync(stream);

        // Assert
        result.Should().Be(expectedHash);
        _hashServiceMock.Verify(
            x => x.ComputeStreamHashAsync(stream, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region IsLineUniqueAsync Tests

    [Fact]
    public async Task IsLineUniqueAsync_WithUniqueLine_ShouldReturnTrue()
    {
        // Arrange
        const string lineHash = "unique-line-hash-123";

        // Act
        var result = await _service.IsLineUniqueAsync(lineHash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsLineUniqueAsync_WithDuplicateLine_ShouldReturnFalse()
    {
        // Arrange
        const string lineHash = "duplicate-line-hash-123";
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "file-hash",
            FileSize = 1000
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        var lineHashRecord = new FileUploadLineHash
        {
            Id = Guid.NewGuid(),
            FileUploadId = fileUpload.Id,
            LineHash = lineHash,
            LineContent = "line content"
        };
        _context.FileUploadLineHashes.Add(lineHashRecord);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsLineUniqueAsync(lineHash);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IsLineUniqueAsync_WithNullOrWhiteSpaceHash_ShouldReturnTrue(string? lineHash)
    {
        // Act
        var result = await _service.IsLineUniqueAsync(lineHash!);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region RecordLineHashAsync Tests

    [Fact]
    public async Task RecordLineHashAsync_WithValidData_ShouldAddToChangeTracker()
    {
        // Arrange
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";
        const string lineContent = "line content";

        var fileUpload = new FileUpload
        {
            Id = fileUploadId,
            FileName = "test.txt",
            FileHash = "file-hash",
            FileSize = 1000
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act
        await _service.RecordLineHashAsync(fileUploadId, lineHash, lineContent);

        // Assert
        var pendingEntries = _context.ChangeTracker.Entries<FileUploadLineHash>()
            .Where(e => e.State == EntityState.Added)
            .ToList();
        pendingEntries.Should().HaveCount(1);
        pendingEntries[0].Entity.LineHash.Should().Be(lineHash);
    }

    [Fact]
    public async Task RecordLineHashAsync_WithDuplicateInChangeTracker_ShouldNotAddAgain()
    {
        // Arrange
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "duplicate-hash-123";
        const string lineContent = "line content";

        var fileUpload = new FileUpload
        {
            Id = fileUploadId,
            FileName = "test.txt",
            FileHash = "file-hash",
            FileSize = 1000
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Add first time
        await _service.RecordLineHashAsync(fileUploadId, lineHash, lineContent);

        // Act - Try to add again
        await _service.RecordLineHashAsync(fileUploadId, lineHash, lineContent);

        // Assert - Should only have one entry
        var pendingEntries = _context.ChangeTracker.Entries<FileUploadLineHash>()
            .Where(e => e.State == EntityState.Added)
            .ToList();
        pendingEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task RecordLineHashAsync_WithDuplicateInDatabase_ShouldNotAdd()
    {
        // Arrange
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "existing-hash-123";
        const string lineContent = "line content";

        var fileUpload = new FileUpload
        {
            Id = fileUploadId,
            FileName = "test.txt",
            FileHash = "file-hash",
            FileSize = 1000
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        var existingLineHash = new FileUploadLineHash
        {
            Id = Guid.NewGuid(),
            FileUploadId = fileUploadId,
            LineHash = lineHash,
            LineContent = lineContent
        };
        _context.FileUploadLineHashes.Add(existingLineHash);
        await _context.SaveChangesAsync();

        // Clear change tracker to simulate new context
        _context.ChangeTracker.Clear();

        // Act
        await _service.RecordLineHashAsync(fileUploadId, lineHash, lineContent);

        // Assert - Should not add duplicate
        var pendingEntries = _context.ChangeTracker.Entries<FileUploadLineHash>()
            .Where(e => e.State == EntityState.Added)
            .ToList();
        pendingEntries.Should().BeEmpty();
    }

    #endregion

    #region UpdateProcessingStatusAsync Tests

    [Fact]
    public async Task UpdateProcessingStatusAsync_WithValidUpload_ShouldUpdateStatus()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Pending
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateProcessingStatusAsync(
            fileUpload.Id, FileUploadStatus.Processing, 1);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.Status.Should().Be(FileUploadStatus.Processing);
        updated.RetryCount.Should().Be(1);
        updated.ProcessingStartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateProcessingStatusAsync_WithNonExistentUpload_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        await _service.UpdateProcessingStatusAsync(
            nonExistentId, FileUploadStatus.Processing, 1);

        // Assert - Should not throw, just log warning
        var upload = await _context.FileUploads.FindAsync(nonExistentId);
        upload.Should().BeNull();
    }

    #endregion

    #region UpdateProcessingSuccessAsync Tests

    [Fact]
    public async Task UpdateProcessingSuccessAsync_WithValidUpload_ShouldUpdateToSuccess()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Processing
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        const int processedLineCount = 50;
        const string storagePath = "minio/bucket/file.txt";

        // Act
        await _service.UpdateProcessingSuccessAsync(
            fileUpload.Id, processedLineCount, storagePath);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.Status.Should().Be(FileUploadStatus.Success);
        updated.ProcessedLineCount.Should().Be(processedLineCount);
        updated.StoragePath.Should().Be(storagePath);
        updated.ProcessingCompletedAt.Should().NotBeNull();
        updated.RetryCount.Should().Be(0);
        updated.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProcessingSuccessAsync_WithNonExistentUpload_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        await _service.UpdateProcessingSuccessAsync(
            nonExistentId, 10, "path");

        // Assert - Should not throw
        var upload = await _context.FileUploads.FindAsync(nonExistentId);
        upload.Should().BeNull();
    }

    #endregion

    #region UpdateProcessingFailureAsync Tests

    [Fact]
    public async Task UpdateProcessingFailureAsync_WithValidUpload_ShouldUpdateToFailed()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Processing
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        const string errorMessage = "Processing failed";
        const int retryCount = 2;

        // Act
        await _service.UpdateProcessingFailureAsync(
            fileUpload.Id, errorMessage, retryCount);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.Status.Should().Be(FileUploadStatus.Failed);
        updated.ErrorMessage.Should().Be(errorMessage);
        updated.RetryCount.Should().Be(retryCount);
        updated.ProcessingCompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateProcessingFailureAsync_WithNonExistentUpload_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        await _service.UpdateProcessingFailureAsync(
            nonExistentId, "error", 1);

        // Assert - Should not throw
        var upload = await _context.FileUploads.FindAsync(nonExistentId);
        upload.Should().BeNull();
    }

    #endregion

    #region GetUploadByIdAsync Tests

    [Fact]
    public async Task GetUploadByIdAsync_WithExistingUpload_ShouldReturnUpload()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUploadByIdAsync(fileUpload.Id);

        // Assert
        result.Should().NotBeNull();
        result!.FileName.Should().Be("test.txt");
    }

    [Fact]
    public async Task GetUploadByIdAsync_WithNonExistentUpload_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.GetUploadByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateCheckpointAsync Tests

    [Fact]
    public async Task UpdateCheckpointAsync_WithValidUpload_ShouldUpdateCheckpoint()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        const int lastCheckpointLine = 100;
        const int processedCount = 95;
        const int failedCount = 3;
        const int skippedCount = 2;

        // Act
        await _service.UpdateCheckpointAsync(
            fileUpload.Id, lastCheckpointLine, processedCount, failedCount, skippedCount);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.LastCheckpointLine.Should().Be(lastCheckpointLine);
        updated.LastCheckpointAt.Should().NotBeNull();
        updated.ProcessedLineCount.Should().Be(processedCount);
        updated.FailedLineCount.Should().Be(failedCount);
        updated.SkippedLineCount.Should().Be(skippedCount);
    }

    [Fact]
    public async Task UpdateCheckpointAsync_WithNonExistentUpload_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        await _service.UpdateCheckpointAsync(
            nonExistentId, 10, 5, 2, 3);

        // Assert - Should not throw
        var upload = await _context.FileUploads.FindAsync(nonExistentId);
        upload.Should().BeNull();
    }

    #endregion

    #region SetTotalLineCountAsync Tests

    [Fact]
    public async Task SetTotalLineCountAsync_WithValidUpload_ShouldSetTotalLineCount()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        const int totalLineCount = 200;

        // Act
        await _service.SetTotalLineCountAsync(fileUpload.Id, totalLineCount);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.TotalLineCount.Should().Be(totalLineCount);
    }

    [Fact]
    public async Task SetTotalLineCountAsync_WithNonExistentUpload_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        await _service.SetTotalLineCountAsync(nonExistentId, 100);

        // Assert - Should not throw
        var upload = await _context.FileUploads.FindAsync(nonExistentId);
        upload.Should().BeNull();
    }

    #endregion

    #region IsUploadIncompleteAsync Tests

    [Theory]
    [InlineData(FileUploadStatus.Pending, true)]
    [InlineData(FileUploadStatus.Processing, true)]
    [InlineData(FileUploadStatus.Success, false)]
    [InlineData(FileUploadStatus.Failed, false)]
    public async Task IsUploadIncompleteAsync_WithDifferentStatuses_ShouldReturnCorrectValue(
        FileUploadStatus status, bool expected)
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = status
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsUploadIncompleteAsync(fileUpload.Id);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task IsUploadIncompleteAsync_WithPartialProcessing_ShouldReturnTrue()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Success,
            TotalLineCount = 100,
            ProcessedLineCount = 50,
            FailedLineCount = 10,
            SkippedLineCount = 5
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.IsUploadIncompleteAsync(fileUpload.Id);

        // Assert
        result.Should().BeTrue(); // 50 + 10 + 5 = 65 < 100
    }

    [Fact]
    public async Task IsUploadIncompleteAsync_WithNonExistentUpload_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.IsUploadIncompleteAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAllUploadsAsync Tests

    [Fact]
    public async Task GetAllUploadsAsync_WithNoFilter_ShouldReturnAllUploads()
    {
        // Arrange
        var uploads = new List<FileUpload>
        {
            new() { Id = Guid.NewGuid(), FileName = "file1.txt", FileHash = "hash1", FileSize = 1000, UploadedAt = DateTime.UtcNow.AddHours(-2) },
            new() { Id = Guid.NewGuid(), FileName = "file2.txt", FileHash = "hash2", FileSize = 2000, UploadedAt = DateTime.UtcNow.AddHours(-1) },
            new() { Id = Guid.NewGuid(), FileName = "file3.txt", FileHash = "hash3", FileSize = 3000, UploadedAt = DateTime.UtcNow }
        };
        _context.FileUploads.AddRange(uploads);
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _service.GetAllUploadsAsync();

        // Assert
        result.Should().HaveCount(3);
        totalCount.Should().Be(3);
        result[0].FileName.Should().Be("file3.txt"); // Most recent first
    }

    [Fact]
    public async Task GetAllUploadsAsync_WithStatusFilter_ShouldReturnFilteredUploads()
    {
        // Arrange
        var uploads = new List<FileUpload>
        {
            new() { Id = Guid.NewGuid(), FileName = "file1.txt", FileHash = "hash1", FileSize = 1000, Status = FileUploadStatus.Success },
            new() { Id = Guid.NewGuid(), FileName = "file2.txt", FileHash = "hash2", FileSize = 2000, Status = FileUploadStatus.Failed },
            new() { Id = Guid.NewGuid(), FileName = "file3.txt", FileHash = "hash3", FileSize = 3000, Status = FileUploadStatus.Success }
        };
        _context.FileUploads.AddRange(uploads);
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _service.GetAllUploadsAsync(
            status: FileUploadStatus.Success);

        // Assert
        result.Should().HaveCount(2);
        totalCount.Should().Be(2);
        result.All(u => u.Status == FileUploadStatus.Success).Should().BeTrue();
    }

    [Fact]
    public async Task GetAllUploadsAsync_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        var uploads = Enumerable.Range(1, 10)
            .Select(i => new FileUpload
            {
                Id = Guid.NewGuid(),
                FileName = $"file{i}.txt",
                FileHash = $"hash{i}",
                FileSize = 1000 * i,
                UploadedAt = DateTime.UtcNow.AddHours(-i)
            })
            .ToList();
        _context.FileUploads.AddRange(uploads);
        await _context.SaveChangesAsync();

        // Act
        var (result, totalCount) = await _service.GetAllUploadsAsync(page: 2, pageSize: 3);

        // Assert
        result.Should().HaveCount(3);
        totalCount.Should().Be(10);
    }

    #endregion

    #region CommitLineHashesAsync Tests

    [Fact]
    public async Task CommitLineHashesAsync_WithNoPendingHashes_ShouldNotThrow()
    {
        // Act & Assert
        await _service.Invoking(s => s.CommitLineHashesAsync())
            .Should().NotThrowAsync();
    }

    #endregion

    #region FindIncompleteUploadsAsync Tests

    [Fact]
    public async Task FindIncompleteUploadsAsync_WithStuckProcessingUpload_ShouldReturnUpload()
    {
        // Arrange
        var stuckUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "stuck.txt",
            FileHash = "hash-stuck",
            FileSize = 1000,
            Status = FileUploadStatus.Processing,
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-35) // Started 35 minutes ago
        };
        _context.FileUploads.Add(stuckUpload);
        await _context.SaveChangesAsync();

        // Act
        var incomplete = await _service.FindIncompleteUploadsAsync(timeoutMinutes: 30);

        // Assert
        incomplete.Should().Contain(u => u.Id == stuckUpload.Id);
    }

    [Fact]
    public async Task FindIncompleteUploadsAsync_WithRecentCheckpoint_ShouldNotReturnUpload()
    {
        // Arrange
        var uploadWithRecentCheckpoint = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "active.txt",
            FileHash = "hash-active",
            FileSize = 1000,
            Status = FileUploadStatus.Processing,
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-35),
            LastCheckpointAt = DateTime.UtcNow.AddMinutes(-5) // Recent checkpoint
        };
        _context.FileUploads.Add(uploadWithRecentCheckpoint);
        await _context.SaveChangesAsync();

        // Act
        var incomplete = await _service.FindIncompleteUploadsAsync(timeoutMinutes: 30);

        // Assert
        incomplete.Should().NotContain(u => u.Id == uploadWithRecentCheckpoint.Id);
    }

    [Fact]
    public async Task FindIncompleteUploadsAsync_WithCompletedUpload_ShouldNotReturnUpload()
    {
        // Arrange
        var completedUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "completed.txt",
            FileHash = "hash-completed",
            FileSize = 1000,
            Status = FileUploadStatus.Success,
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-35)
        };
        _context.FileUploads.Add(completedUpload);
        await _context.SaveChangesAsync();

        // Act
        var incomplete = await _service.FindIncompleteUploadsAsync(timeoutMinutes: 30);

        // Assert
        incomplete.Should().NotContain(u => u.Id == completedUpload.Id);
    }

    [Fact]
    public async Task FindIncompleteUploadsAsync_WithNoStuckUploads_ShouldReturnEmptyList()
    {
        // Arrange - No stuck uploads

        // Act
        var incomplete = await _service.FindIncompleteUploadsAsync(timeoutMinutes: 30);

        // Assert
        incomplete.Should().BeEmpty();
    }

    #endregion

    #region UpdateProcessingResultAsync Tests

    [Fact]
    public async Task UpdateProcessingResultAsync_WithAllLinesProcessed_ShouldSetSuccessStatus()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Processing,
            TotalLineCount = 10
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act - All 10 lines processed successfully
        await _service.UpdateProcessingResultAsync(
            fileUpload.Id,
            processedCount: 10,
            failedCount: 0,
            skippedCount: 0);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.Status.Should().Be(FileUploadStatus.Success);
        updated.ProcessedLineCount.Should().Be(10);
        updated.ProcessingCompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateProcessingResultAsync_WithAllLinesProcessedAndFailures_ShouldSetPartiallyCompletedStatus()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Processing,
            TotalLineCount = 10
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act - All 10 lines processed, but 2 failed
        await _service.UpdateProcessingResultAsync(
            fileUpload.Id,
            processedCount: 8,
            failedCount: 2,
            skippedCount: 0);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.Status.Should().Be(FileUploadStatus.PartiallyCompleted);
        updated.ProcessedLineCount.Should().Be(8);
        updated.FailedLineCount.Should().Be(2);
    }

    [Fact]
    public async Task UpdateProcessingResultAsync_WithPartialProcessing_ShouldKeepProcessingStatus()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Processing,
            TotalLineCount = 10
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act - Only 5 of 10 lines processed
        await _service.UpdateProcessingResultAsync(
            fileUpload.Id,
            processedCount: 5,
            failedCount: 0,
            skippedCount: 0);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.Status.Should().Be(FileUploadStatus.Processing);
        updated.ProcessedLineCount.Should().Be(5);
    }

    [Fact]
    public async Task UpdateProcessingResultAsync_WithPartialProcessingAndFailures_ShouldSetPartiallyCompletedStatus()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Failed, // Not Processing or Pending, so status can be changed
            TotalLineCount = 10
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act - Partial processing with failures, status is not Processing or Pending
        await _service.UpdateProcessingResultAsync(
            fileUpload.Id,
            processedCount: 3,
            failedCount: 2,
            skippedCount: 0);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.Status.Should().Be(FileUploadStatus.PartiallyCompleted);
    }

    [Fact]
    public async Task UpdateProcessingResultAsync_WithNonExistentUpload_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await _service.Invoking(s => s.UpdateProcessingResultAsync(
            nonExistentId, 10, 0, 0))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateProcessingResultAsync_ShouldUpdateLastCheckpointWhenAllProcessed()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "test.txt",
            FileHash = "hash-123",
            FileSize = 1000,
            Status = FileUploadStatus.Processing,
            TotalLineCount = 10
        };
        _context.FileUploads.Add(fileUpload);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateProcessingResultAsync(
            fileUpload.Id,
            processedCount: 10,
            failedCount: 0,
            skippedCount: 0);

        // Assert
        var updated = await _context.FileUploads.FindAsync(fileUpload.Id);
        updated!.LastCheckpointLine.Should().Be(9); // 0-based index, last line is 9
        updated.LastCheckpointAt.Should().NotBeNull();
    }

    #endregion
}

