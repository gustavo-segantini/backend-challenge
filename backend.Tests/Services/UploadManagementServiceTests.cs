using CnabApi.Common;
using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Models.Responses;
using CnabApi.Services;
using CnabApi.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for UploadManagementService.
/// Tests upload management operations including queries, validation, and resume operations.
/// </summary>
public class UploadManagementServiceTests
{
    private readonly Mock<IFileUploadTrackingService> _fileUploadTrackingServiceMock;
    private readonly Mock<IUploadQueueService> _uploadQueueServiceMock;
    private readonly Mock<ILogger<UploadManagementService>> _loggerMock;
    private readonly CnabDbContext _dbContext;
    private readonly UploadManagementService _service;

    public UploadManagementServiceTests()
    {
        _fileUploadTrackingServiceMock = new Mock<IFileUploadTrackingService>();
        _uploadQueueServiceMock = new Mock<IUploadQueueService>();
        _loggerMock = new Mock<ILogger<UploadManagementService>>();
        
        var options = new DbContextOptionsBuilder<CnabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new CnabDbContext(options);
        
        _service = new UploadManagementService(
            _fileUploadTrackingServiceMock.Object,
            _uploadQueueServiceMock.Object,
            _dbContext,
            _loggerMock.Object);
    }

    #region GetAllUploadsAsync Tests

    [Fact]
    public async Task GetAllUploadsAsync_WithDefaultParameters_ShouldReturnPagedResponse()
    {
        // Arrange
        var uploads = new List<FileUpload>
        {
            new FileUpload
            {
                Id = Guid.NewGuid(),
                FileName = "test1.txt",
                Status = FileUploadStatus.Success,
                FileSize = 100,
                TotalLineCount = 10,
                ProcessedLineCount = 10,
                UploadedAt = DateTime.UtcNow
            }
        };
        var totalCount = 1;

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetAllUploadsAsync(1, 50, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((uploads, totalCount));

        // Act
        var result = await _service.GetAllUploadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);
        result.TotalPages.Should().Be(1);
        result.Items.First().FileName.Should().Be("test1.txt");
        result.Items.First().Status.Should().Be("Success");
    }

    [Fact]
    public async Task GetAllUploadsAsync_WithCustomPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var uploads = new List<FileUpload>
        {
            new FileUpload
            {
                Id = Guid.NewGuid(),
                FileName = "test2.txt",
                Status = FileUploadStatus.Processing,
                FileSize = 200,
                TotalLineCount = 20,
                ProcessedLineCount = 10,
                UploadedAt = DateTime.UtcNow
            }
        };
        var totalCount = 25;
        var page = 2;
        var pageSize = 10;

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetAllUploadsAsync(page, pageSize, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((uploads, totalCount));

        // Act
        var result = await _service.GetAllUploadsAsync(page, pageSize);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(3); // Ceiling(25/10) = 3
    }

    [Fact]
    public async Task GetAllUploadsAsync_WithStatusFilter_ShouldFilterByStatus()
    {
        // Arrange
        var uploads = new List<FileUpload>
        {
            new FileUpload
            {
                Id = Guid.NewGuid(),
                FileName = "test3.txt",
                Status = FileUploadStatus.Failed,
                FileSize = 300,
                TotalLineCount = 30,
                ProcessedLineCount = 0,
                UploadedAt = DateTime.UtcNow
            }
        };
        var totalCount = 1;

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetAllUploadsAsync(1, 50, FileUploadStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync((uploads, totalCount));

        // Act
        var result = await _service.GetAllUploadsAsync(status: FileUploadStatus.Failed);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Status.Should().Be("Failed");
    }

    [Fact]
    public async Task GetAllUploadsAsync_WithEmptyResults_ShouldReturnEmptyPagedResponse()
    {
        // Arrange
        var uploads = new List<FileUpload>();
        var totalCount = 0;

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetAllUploadsAsync(1, 50, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((uploads, totalCount));

        // Act
        var result = await _service.GetAllUploadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    #endregion

    #region GetIncompleteUploadsAsync Tests

    [Fact]
    public async Task GetIncompleteUploadsAsync_WithDefaultTimeout_ShouldReturnIncompleteUploads()
    {
        // Arrange
        var incompleteUploads = new List<FileUpload>
        {
            new FileUpload
            {
                Id = Guid.NewGuid(),
                FileName = "incomplete1.txt",
                Status = FileUploadStatus.Processing,
                FileSize = 100,
                TotalLineCount = 10,
                ProcessedLineCount = 5,
                LastCheckpointLine = 5,
                UploadedAt = DateTime.UtcNow.AddMinutes(-35)
            }
        };

        _fileUploadTrackingServiceMock
            .Setup(x => x.FindIncompleteUploadsAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteUploads);

        // Act
        var result = await _service.GetIncompleteUploadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IncompleteUploads.Should().HaveCount(1);
        result.Data.Count.Should().Be(1);
        result.Data.IncompleteUploads.First().FileName.Should().Be("incomplete1.txt");
        result.Data.IncompleteUploads.First().Status.Should().Be("Processing");
    }

    [Fact]
    public async Task GetIncompleteUploadsAsync_WithCustomTimeout_ShouldUseCustomTimeout()
    {
        // Arrange
        var incompleteUploads = new List<FileUpload>
        {
            new FileUpload
            {
                Id = Guid.NewGuid(),
                FileName = "incomplete2.txt",
                Status = FileUploadStatus.Processing,
                FileSize = 200,
                TotalLineCount = 20,
                ProcessedLineCount = 10,
                LastCheckpointLine = 10,
                UploadedAt = DateTime.UtcNow.AddMinutes(-65)
            }
        };
        var timeoutMinutes = 60;

        _fileUploadTrackingServiceMock
            .Setup(x => x.FindIncompleteUploadsAsync(timeoutMinutes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteUploads);

        // Act
        var result = await _service.GetIncompleteUploadsAsync(timeoutMinutes);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IncompleteUploads.Should().HaveCount(1);
        result.Data.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetIncompleteUploadsAsync_WithNoIncompleteUploads_ShouldReturnEmptyResponse()
    {
        // Arrange
        var incompleteUploads = new List<FileUpload>();

        _fileUploadTrackingServiceMock
            .Setup(x => x.FindIncompleteUploadsAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteUploads);

        // Act
        var result = await _service.GetIncompleteUploadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IncompleteUploads.Should().BeEmpty();
        result.Data.Count.Should().Be(0);
    }

    #endregion

    #region ResumeUploadAsync Tests

    [Fact]
    public async Task ResumeUploadAsync_WithValidIncompleteUpload_ShouldEnqueueAndReturnSuccess()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var upload = new FileUpload
        {
            Id = uploadId,
            FileName = "resume-test.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 5,
            LastCheckpointLine = 5,
            StoragePath = "cnab-20250101-120000-123.txt",
            UploadedAt = DateTime.UtcNow.AddMinutes(-35)
        };
        var messageId = "msg-123";

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetUploadByIdAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upload);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsUploadIncompleteAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(uploadId, upload.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageId);

        // Act
        var result = await _service.ResumeUploadAsync(uploadId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Message.Should().Be("Upload re-enqueued for processing");
        result.Data.UploadId.Should().Be(uploadId);
        result.Data.WillResumeFromLine.Should().Be(5);
        result.Data.TotalLineCount.Should().Be(10);
        result.Data.ProcessedLineCount.Should().Be(5);

        _uploadQueueServiceMock.Verify(
            x => x.EnqueueUploadAsync(uploadId, upload.StoragePath, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResumeUploadAsync_WithNonExistentUpload_ShouldReturnFailure()
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetUploadByIdAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileUpload?)null);

        // Act
        var result = await _service.ResumeUploadAsync(uploadId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task ResumeUploadAsync_WithCompleteUpload_ShouldReturnFailure()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var upload = new FileUpload
        {
            Id = uploadId,
            FileName = "complete.txt",
            Status = FileUploadStatus.Success,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 10,
            StoragePath = "cnab-20250101-120000-123.txt",
            UploadedAt = DateTime.UtcNow
        };

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetUploadByIdAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upload);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsUploadIncompleteAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ResumeUploadAsync(uploadId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not incomplete or cannot be resumed");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task ResumeUploadAsync_WithMissingStoragePath_ShouldReturnFailure()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var upload = new FileUpload
        {
            Id = uploadId,
            FileName = "no-storage.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 5,
            LastCheckpointLine = 5,
            StoragePath = null,
            UploadedAt = DateTime.UtcNow.AddMinutes(-35)
        };

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetUploadByIdAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upload);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsUploadIncompleteAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ResumeUploadAsync(uploadId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not have a storage path");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task ResumeUploadAsync_WithEmptyStoragePath_ShouldReturnFailure()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var upload = new FileUpload
        {
            Id = uploadId,
            FileName = "empty-storage.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 5,
            LastCheckpointLine = 5,
            StoragePath = string.Empty,
            UploadedAt = DateTime.UtcNow.AddMinutes(-35)
        };

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetUploadByIdAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upload);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsUploadIncompleteAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ResumeUploadAsync(uploadId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("does not have a storage path");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task ResumeUploadAsync_WhenEnqueueFails_ShouldReturnFailure()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var upload = new FileUpload
        {
            Id = uploadId,
            FileName = "enqueue-fail.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 5,
            LastCheckpointLine = 5,
            StoragePath = "cnab-20250101-120000-123.txt",
            UploadedAt = DateTime.UtcNow.AddMinutes(-35)
        };
        var errorMessage = "Queue service unavailable";

        _fileUploadTrackingServiceMock
            .Setup(x => x.GetUploadByIdAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(upload);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsUploadIncompleteAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(uploadId, upload.StoragePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(errorMessage));

        // Act
        var result = await _service.ResumeUploadAsync(uploadId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to resume upload");
        result.ErrorMessage.Should().Contain(errorMessage);
        result.Data.Should().BeNull();
    }

    #endregion

    #region ResumeAllIncompleteUploadsAsync Tests

    [Fact]
    public async Task ResumeAllIncompleteUploadsAsync_WithValidUploads_ShouldResumeAll()
    {
        // Arrange
        var upload1 = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "resume1.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 5,
            LastCheckpointLine = 5,
            StoragePath = "cnab-20250101-120000-123.txt",
            UploadedAt = DateTime.UtcNow.AddMinutes(-35)
        };
        var upload2 = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "resume2.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 200,
            TotalLineCount = 20,
            ProcessedLineCount = 10,
            LastCheckpointLine = 10,
            StoragePath = "cnab-20250101-120000-456.txt",
            UploadedAt = DateTime.UtcNow.AddMinutes(-40)
        };
        var incompleteUploads = new List<FileUpload> { upload1, upload2 };

        _fileUploadTrackingServiceMock
            .Setup(x => x.FindIncompleteUploadsAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteUploads);

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(upload1.Id, upload1.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-1");

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(upload2.Id, upload2.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-2");

        // Act
        var result = await _service.ResumeAllIncompleteUploadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ResumedCount.Should().Be(2);
        result.Data.ResumedUploads.Should().HaveCount(2);
        result.Data.Errors.Should().BeNull();
        result.Data.Message.Should().Contain("Resumed 2 incomplete upload(s)");

        var resumed1 = result.Data.ResumedUploads.First(u => u.UploadId == upload1.Id);
        resumed1.FileName.Should().Be("resume1.txt");
        resumed1.WillResumeFromLine.Should().Be(5);

        var resumed2 = result.Data.ResumedUploads.First(u => u.UploadId == upload2.Id);
        resumed2.FileName.Should().Be("resume2.txt");
        resumed2.WillResumeFromLine.Should().Be(10);
    }

    [Fact]
    public async Task ResumeAllIncompleteUploadsAsync_WithNoIncompleteUploads_ShouldReturnEmptyResult()
    {
        // Arrange
        var incompleteUploads = new List<FileUpload>();

        _fileUploadTrackingServiceMock
            .Setup(x => x.FindIncompleteUploadsAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteUploads);

        // Act
        var result = await _service.ResumeAllIncompleteUploadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ResumedCount.Should().Be(0);
        result.Data.ResumedUploads.Should().BeEmpty();
        result.Data.Errors.Should().BeNull();
        result.Data.Message.Should().Contain("Resumed 0 incomplete upload(s)");
    }

    [Fact]
    public async Task ResumeAllIncompleteUploadsAsync_WithPartialFailures_ShouldReturnPartialResults()
    {
        // Arrange
        var upload1 = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "resume1.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 5,
            LastCheckpointLine = 5,
            StoragePath = "cnab-20250101-120000-123.txt",
            UploadedAt = DateTime.UtcNow.AddMinutes(-35)
        };
        var upload2 = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "no-storage.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 200,
            TotalLineCount = 20,
            ProcessedLineCount = 10,
            LastCheckpointLine = 10,
            StoragePath = null, // Missing storage path
            UploadedAt = DateTime.UtcNow.AddMinutes(-40)
        };
        var incompleteUploads = new List<FileUpload> { upload1, upload2 };

        _fileUploadTrackingServiceMock
            .Setup(x => x.FindIncompleteUploadsAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteUploads);

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(upload1.Id, upload1.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-1");

        // Act
        var result = await _service.ResumeAllIncompleteUploadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ResumedCount.Should().Be(1);
        result.Data.ResumedUploads.Should().HaveCount(1);
        result.Data.Errors.Should().NotBeNull();
        result.Data.Errors.Should().HaveCount(1);
        result.Data.Errors!.First().Should().Contain("does not have a storage path");
        result.Data.Message.Should().Contain("Resumed 1 incomplete upload(s)");
    }

    [Fact]
    public async Task ResumeAllIncompleteUploadsAsync_WithEnqueueFailures_ShouldReturnErrors()
    {
        // Arrange
        var upload1 = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "resume1.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 5,
            LastCheckpointLine = 5,
            StoragePath = "cnab-20250101-120000-123.txt",
            UploadedAt = DateTime.UtcNow.AddMinutes(-35)
        };
        var upload2 = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "enqueue-fail.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 200,
            TotalLineCount = 20,
            ProcessedLineCount = 10,
            LastCheckpointLine = 10,
            StoragePath = "cnab-20250101-120000-456.txt",
            UploadedAt = DateTime.UtcNow.AddMinutes(-40)
        };
        var incompleteUploads = new List<FileUpload> { upload1, upload2 };
        var errorMessage = "Queue service unavailable";

        _fileUploadTrackingServiceMock
            .Setup(x => x.FindIncompleteUploadsAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteUploads);

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(upload1.Id, upload1.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-1");

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(upload2.Id, upload2.StoragePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(errorMessage));

        // Act
        var result = await _service.ResumeAllIncompleteUploadsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ResumedCount.Should().Be(1);
        result.Data.ResumedUploads.Should().HaveCount(1);
        result.Data.Errors.Should().NotBeNull();
        result.Data.Errors.Should().HaveCount(1);
        result.Data.Errors!.First().Should().Contain("Error resuming upload");
        result.Data.Errors.First().Should().Contain(errorMessage);
    }

    [Fact]
    public async Task ResumeAllIncompleteUploadsAsync_WithCustomTimeout_ShouldUseCustomTimeout()
    {
        // Arrange
        var upload = new FileUpload
        {
            Id = Guid.NewGuid(),
            FileName = "resume-custom.txt",
            Status = FileUploadStatus.Processing,
            FileSize = 100,
            TotalLineCount = 10,
            ProcessedLineCount = 5,
            LastCheckpointLine = 5,
            StoragePath = "cnab-20250101-120000-123.txt",
            UploadedAt = DateTime.UtcNow.AddMinutes(-65)
        };
        var incompleteUploads = new List<FileUpload> { upload };
        var timeoutMinutes = 60;

        _fileUploadTrackingServiceMock
            .Setup(x => x.FindIncompleteUploadsAsync(timeoutMinutes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteUploads);

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(upload.Id, upload.StoragePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-1");

        // Act
        var result = await _service.ResumeAllIncompleteUploadsAsync(timeoutMinutes);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ResumedCount.Should().Be(1);
        result.Data.ResumedUploads.Should().HaveCount(1);
    }

    #endregion
}

