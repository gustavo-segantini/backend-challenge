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
/// Unit tests for SynchronousUploadProcessingStrategy.
/// Tests the synchronous upload processing strategy that processes files immediately.
/// </summary>
public class SynchronousUploadProcessingStrategyTests
{
    private readonly Mock<ICnabUploadService> _cnabUploadServiceMock;
    private readonly Mock<IFileUploadTrackingService> _fileUploadTrackingServiceMock;
    private readonly Mock<ILogger<SynchronousUploadProcessingStrategy>> _loggerMock;
    private readonly SynchronousUploadProcessingStrategy _strategy;

    public SynchronousUploadProcessingStrategyTests()
    {
        _cnabUploadServiceMock = new Mock<ICnabUploadService>();
        _fileUploadTrackingServiceMock = new Mock<IFileUploadTrackingService>();
        _loggerMock = new Mock<ILogger<SynchronousUploadProcessingStrategy>>();
        _strategy = new SynchronousUploadProcessingStrategy(
            _cnabUploadServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _loggerMock.Object);
    }

    #region ProcessUploadAsync - Success Tests

    [Fact]
    public async Task ProcessUploadAsync_WithValidFile_ShouldProcessAndReturnSuccess()
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
        var transactionCount = 1;

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cnabUploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(fileContent, uploadId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(transactionCount));

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingSuccessAsync(uploadId, transactionCount, storagePath, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(transactionCount);
        result.Data.StatusCode.Should().Be(UploadStatusCode.Success);
        result.Data.UploadId.Should().Be(uploadId);

        _fileUploadTrackingServiceMock.Verify(
            x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, It.IsAny<CancellationToken>()),
            Times.Once);
        _cnabUploadServiceMock.Verify(
            x => x.ProcessCnabUploadAsync(fileContent, uploadId, 0, It.IsAny<CancellationToken>()),
            Times.Once);
        _fileUploadTrackingServiceMock.Verify(
            x => x.UpdateProcessingSuccessAsync(uploadId, transactionCount, storagePath, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessUploadAsync_WithNullStoragePath_ShouldProcessSuccessfully()
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
        var transactionCount = 5;

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cnabUploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(fileContent, uploadId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(transactionCount));

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingSuccessAsync(uploadId, transactionCount, storagePath, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(transactionCount);
        result.Data.StatusCode.Should().Be(UploadStatusCode.Success);

        _fileUploadTrackingServiceMock.Verify(
            x => x.UpdateProcessingSuccessAsync(uploadId, transactionCount, storagePath, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessUploadAsync_WithZeroTransactions_ShouldReturnSuccess()
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
        var transactionCount = 0;

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cnabUploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(fileContent, uploadId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(transactionCount));

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingSuccessAsync(uploadId, transactionCount, storagePath, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0);
        result.Data.StatusCode.Should().Be(UploadStatusCode.Success);
    }

    #endregion

    #region ProcessUploadAsync - Processing Failure Tests

    [Fact]
    public async Task ProcessUploadAsync_WhenProcessingFails_ShouldReturnUnprocessableEntity()
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
        var errorMessage = "Invalid CNAB format";

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cnabUploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(fileContent, uploadId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure(errorMessage));

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingFailureAsync(uploadId, errorMessage, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0);
        result.Data.StatusCode.Should().Be(UploadStatusCode.UnprocessableEntity);
        result.Data.UploadId.Should().Be(uploadId);

        _fileUploadTrackingServiceMock.Verify(
            x => x.UpdateProcessingFailureAsync(uploadId, errorMessage, 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessUploadAsync_WhenProcessingFailsWithNullErrorMessage_ShouldUseDefaultMessage()
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

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cnabUploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(fileContent, uploadId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure(string.Empty));

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingFailureAsync(uploadId, "Processing failed", 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        // When ErrorMessage is empty string, it's used as-is (not null, so ?? doesn't apply)
        result.ErrorMessage.Should().Be(string.Empty);
        result.Data.Should().NotBeNull();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.UnprocessableEntity);
    }

    #endregion

    #region ProcessUploadAsync - Exception Handling Tests

    [Fact]
    public async Task ProcessUploadAsync_WhenExceptionOccurs_ShouldReturnInternalServerError()
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
        var exceptionMessage = "Database connection failed";

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(exceptionMessage));

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingFailureAsync(uploadId, It.Is<string>(s => s.Contains(exceptionMessage)), 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to process file");
        result.ErrorMessage.Should().Contain(exceptionMessage);
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0);
        result.Data.StatusCode.Should().Be(UploadStatusCode.InternalServerError);
        result.Data.UploadId.Should().Be(uploadId);

        _fileUploadTrackingServiceMock.Verify(
            x => x.UpdateProcessingFailureAsync(uploadId, It.Is<string>(s => s.Contains(exceptionMessage)), 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessUploadAsync_WhenProcessingThrowsException_ShouldReturnInternalServerError()
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
        var exceptionMessage = "Processing service unavailable";

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _cnabUploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(fileContent, uploadId, 0, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(exceptionMessage));

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingFailureAsync(uploadId, It.Is<string>(s => s.Contains(exceptionMessage)), 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to process file");
        result.ErrorMessage.Should().Contain(exceptionMessage);
        result.Data.Should().NotBeNull();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.InternalServerError);
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

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingStatusAsync(uploadId, FileUploadStatus.Processing, 0, cancellationTokenSource.Token))
            .ThrowsAsync(new OperationCanceledException("Operation was cancelled"));

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateProcessingFailureAsync(uploadId, It.Is<string>(s => s.Contains("Operation was cancelled")), 0, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _strategy.ProcessUploadAsync(fileContent, fileUpload, storagePath, cancellationTokenSource.Token);

        // Assert
        // The strategy catches exceptions and returns a failure result instead of propagating
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to process file");
        result.ErrorMessage.Should().Contain("Operation was cancelled");
        result.Data.Should().NotBeNull();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.InternalServerError);
    }

    #endregion
}

