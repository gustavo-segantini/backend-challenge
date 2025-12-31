using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Interfaces;
using CnabApi.Services.UploadProcessing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CnabApi.Tests.Services.UploadProcessing;

/// <summary>
/// Unit tests for AsynchronousUploadProcessingStrategy.
/// Tests the asynchronous upload processing strategy that enqueues files for background processing.
/// </summary>
public class AsynchronousUploadProcessingStrategyTests
{
    private readonly Mock<IUploadQueueService> _uploadQueueServiceMock;
    private readonly Mock<ILogger<AsynchronousUploadProcessingStrategy>> _loggerMock;
    private readonly AsynchronousUploadProcessingStrategy _strategy;

    public AsynchronousUploadProcessingStrategyTests()
    {
        _uploadQueueServiceMock = new Mock<IUploadQueueService>();
        _loggerMock = new Mock<ILogger<AsynchronousUploadProcessingStrategy>>();
        _strategy = new AsynchronousUploadProcessingStrategy(
            _uploadQueueServiceMock.Object,
            _loggerMock.Object);
    }

    #region ProcessUploadAsync - Success Tests

    [Fact]
    public async Task ProcessUploadAsync_WithValidFile_ShouldEnqueueAndReturnAccepted()
    {
        // Arrange
        var fileContent = "32019030100000142000962067601712345678901234567890123456789012345678901234567890";
        var uploadId = Guid.NewGuid();
        var fileUpload = new FileUpload
        {
            Id = uploadId,
            FileName = "test.txt",
            FileHash = "abc123",
            FileSize = 100,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };
        var storagePath = "cnab-20250101-120000-123.txt";
        var messageId = "msg-123";

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(uploadId, storagePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageId);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0);
        result.Data.StatusCode.Should().Be(UploadStatusCode.Accepted);
        result.Data.UploadId.Should().Be(uploadId);

        _uploadQueueServiceMock.Verify(
            x => x.EnqueueUploadAsync(uploadId, storagePath, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessUploadAsync_WithNullStoragePath_ShouldUseEmptyString()
    {
        // Arrange
        var fileContent = "32019030100000142000962067601712345678901234567890123456789012345678901234567890";
        var uploadId = Guid.NewGuid();
        var fileUpload = new FileUpload
        {
            Id = uploadId,
            FileName = "test.txt",
            FileHash = "abc123",
            FileSize = 100,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };
        string? storagePath = null;
        var messageId = "msg-123";

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(uploadId, string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageId);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.Accepted);

        _uploadQueueServiceMock.Verify(
            x => x.EnqueueUploadAsync(uploadId, string.Empty, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessUploadAsync_WithEmptyStoragePath_ShouldUseEmptyString()
    {
        // Arrange
        var fileContent = "32019030100000142000962067601712345678901234567890123456789012345678901234567890";
        var uploadId = Guid.NewGuid();
        var fileUpload = new FileUpload
        {
            Id = uploadId,
            FileName = "test.txt",
            FileHash = "abc123",
            FileSize = 100,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };
        var storagePath = string.Empty;
        var messageId = "msg-123";

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(uploadId, string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageId);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.Accepted);

        _uploadQueueServiceMock.Verify(
            x => x.EnqueueUploadAsync(uploadId, string.Empty, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ProcessUploadAsync - Error Handling Tests

    [Fact]
    public async Task ProcessUploadAsync_WhenEnqueueFails_ShouldReturnInternalServerError()
    {
        // Arrange
        var fileContent = "32019030100000142000962067601712345678901234567890123456789012345678901234567890";
        var uploadId = Guid.NewGuid();
        var fileUpload = new FileUpload
        {
            Id = uploadId,
            FileName = "test.txt",
            FileHash = "abc123",
            FileSize = 100,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };
        var storagePath = "cnab-20250101-120000-123.txt";
        var errorMessage = "Queue service unavailable";

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(uploadId, storagePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(errorMessage));

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to enqueue file for processing");
        result.ErrorMessage.Should().Contain(errorMessage);
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0);
        result.Data.StatusCode.Should().Be(UploadStatusCode.InternalServerError);
        result.Data.UploadId.Should().Be(uploadId);
    }

    [Fact]
    public async Task ProcessUploadAsync_WhenEnqueueThrowsTimeoutException_ShouldReturnInternalServerError()
    {
        // Arrange
        var fileContent = "32019030100000142000962067601712345678901234567890123456789012345678901234567890";
        var uploadId = Guid.NewGuid();
        var fileUpload = new FileUpload
        {
            Id = uploadId,
            FileName = "test.txt",
            FileHash = "abc123",
            FileSize = 100,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };
        var storagePath = "cnab-20250101-120000-123.txt";

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(uploadId, storagePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Queue timeout"));

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to enqueue file for processing");
        result.Data.Should().NotBeNull();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.InternalServerError);
        result.Data.UploadId.Should().Be(uploadId);
    }

    #endregion

    #region ProcessUploadAsync - Cancellation Tests

    [Fact]
    public async Task ProcessUploadAsync_WhenCancelled_ShouldReturnInternalServerError()
    {
        // Arrange
        var fileContent = "32019030100000142000962067601712345678901234567890123456789012345678901234567890";
        var uploadId = Guid.NewGuid();
        var fileUpload = new FileUpload
        {
            Id = uploadId,
            FileName = "test.txt",
            FileHash = "abc123",
            FileSize = 100,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };
        var storagePath = "cnab-20250101-120000-123.txt";
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(uploadId, storagePath, cancellationTokenSource.Token))
            .ThrowsAsync(new OperationCanceledException("Operation was cancelled"));

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath, cancellationTokenSource.Token);

        // Assert
        // The strategy catches exceptions and returns a failure result instead of propagating
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to enqueue file for processing");
        result.ErrorMessage.Should().Contain("Operation was cancelled");
        result.Data.Should().NotBeNull();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.InternalServerError);
    }

    #endregion
}

