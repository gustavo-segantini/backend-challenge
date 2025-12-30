using CnabApi.Services.Interfaces;
using CnabApi.Services.LineProcessing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CnabApi.Tests.Services.LineProcessing;

/// <summary>
/// Unit tests for CheckpointManager.
/// </summary>
public class CheckpointManagerTests
{
    private readonly CheckpointManager _checkpointManager;
    private readonly Mock<IFileUploadTrackingService> _fileUploadTrackingServiceMock;
    private readonly Mock<ILogger> _loggerMock;

    public CheckpointManagerTests()
    {
        _checkpointManager = new CheckpointManager();
        _fileUploadTrackingServiceMock = new Mock<IFileUploadTrackingService>();
        _loggerMock = new Mock<ILogger>();
    }

    #region ShouldSaveCheckpoint Tests

    [Theory]
    [InlineData(0, 10, false)] // Zero processed, should not save
    [InlineData(5, 10, false)] // Not at interval
    [InlineData(10, 10, true)] // At interval
    [InlineData(20, 10, true)] // At interval
    [InlineData(30, 10, true)] // At interval
    [InlineData(15, 10, false)] // Not at interval
    [InlineData(100, 50, true)] // At interval
    [InlineData(50, 50, true)] // At interval
    [InlineData(25, 50, false)] // Not at interval
    public void ShouldSaveCheckpoint_WithDifferentCounts_ShouldReturnCorrectValue(
        int totalProcessed,
        int checkpointInterval,
        bool expected)
    {
        // Act
        var result = _checkpointManager.ShouldSaveCheckpoint(totalProcessed, checkpointInterval);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldSaveCheckpoint_WithZeroInterval_ShouldThrowDivideByZeroException()
    {
        // Act & Assert - Division by zero should throw
        var act = () => _checkpointManager.ShouldSaveCheckpoint(10, 0);
        act.Should().Throw<DivideByZeroException>();
    }

    #endregion

    #region SaveCheckpointAsync Tests

    [Fact]
    public async Task SaveCheckpointAsync_WithValidData_ShouldCallUpdateCheckpointAsync()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var lastCheckpointLine = 10;
        var processedCount = 10;
        var failedCount = 0;
        var skippedCount = 0;

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateCheckpointAsync(
                uploadId,
                lastCheckpointLine,
                processedCount,
                failedCount,
                skippedCount,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _checkpointManager.SaveCheckpointAsync(
            uploadId,
            lastCheckpointLine,
            processedCount,
            failedCount,
            skippedCount,
            _fileUploadTrackingServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _fileUploadTrackingServiceMock.Verify(
            x => x.UpdateCheckpointAsync(
                uploadId,
                lastCheckpointLine,
                processedCount,
                failedCount,
                skippedCount,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveCheckpointAsync_WhenUpdateThrowsException_ShouldLogWarningAndNotThrow()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var exception = new Exception("Database error");

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateCheckpointAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var act = async () => await _checkpointManager.SaveCheckpointAsync(
            uploadId,
            10,
            10,
            0,
            0,
            _fileUploadTrackingServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert - Should not throw (checkpoints are best effort)
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveCheckpointAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateCheckpointAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = async () => await _checkpointManager.SaveCheckpointAsync(
            uploadId,
            10,
            10,
            0,
            0,
            _fileUploadTrackingServiceMock.Object,
            _loggerMock.Object,
            cts.Token);

        await act.Should().NotThrowAsync(); // Should handle cancellation gracefully
    }

    [Theory]
    [InlineData(10, 5, 2, 3)]
    [InlineData(100, 50, 10, 40)]
    [InlineData(0, 0, 0, 0)]
    public async Task SaveCheckpointAsync_WithDifferentCounts_ShouldPassCorrectValues(
        int processedCount,
        int failedCount,
        int skippedCount,
        int lastCheckpointLine)
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        _fileUploadTrackingServiceMock
            .Setup(x => x.UpdateCheckpointAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _checkpointManager.SaveCheckpointAsync(
            uploadId,
            lastCheckpointLine,
            processedCount,
            failedCount,
            skippedCount,
            _fileUploadTrackingServiceMock.Object,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        _fileUploadTrackingServiceMock.Verify(
            x => x.UpdateCheckpointAsync(
                uploadId,
                lastCheckpointLine,
                processedCount,
                failedCount,
                skippedCount,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

