using CnabApi.Common;
using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Interfaces;
using CnabApi.Services.LineProcessing;
using CnabApi.Services.UnitOfWork;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;

namespace CnabApi.Tests.Services.LineProcessing;

/// <summary>
/// Unit tests for LineProcessor.
/// Tests line processing with various scenarios including duplicates, parsing failures, and retries.
/// </summary>
public class LineProcessorTests : IDisposable
{
    private readonly CnabDbContext _context;
    private readonly Mock<ITransactionService> _transactionServiceMock;
    private readonly Mock<IFileUploadTrackingService> _fileUploadTrackingServiceMock;
    private readonly Mock<ICnabParserService> _parserServiceMock;
    private readonly Mock<IHashService> _hashServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly LineProcessor _lineProcessor;

    public LineProcessorTests()
    {
        var options = new DbContextOptionsBuilder<CnabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CnabDbContext(options);
        _transactionServiceMock = new Mock<ITransactionService>();
        _fileUploadTrackingServiceMock = new Mock<IFileUploadTrackingService>();
        _parserServiceMock = new Mock<ICnabParserService>();
        _hashServiceMock = new Mock<IHashService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger>();

        _lineProcessor = new LineProcessor();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region ProcessLineAsync - Success Cases

    [Fact]
    public async Task ProcessLineAsync_WithValidLine_ShouldReturnSuccess()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";

        var transaction = new Transaction
        {
            NatureCode = "3",
            Amount = 142.00m,
            Cpf = "09620676017"
        };

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _parserServiceMock
            .Setup(x => x.ParseCnabLine(line, 0))
            .Returns(Result<Transaction>.Success(transaction));

        _transactionServiceMock
            .Setup(x => x.AddTransactionToContextAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Success(transaction));

        _fileUploadTrackingServiceMock
            .Setup(x => x.RecordLineHashAsync(
                fileUploadId,
                lineHash,
                line,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<Transaction>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Transaction>>, CancellationToken>(async (operation, ct) => await operation());

        // Act
        var result = await _lineProcessor.ProcessLineAsync(
            line,
            0,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(LineProcessingResult.Success);
        _hashServiceMock.Verify(x => x.ComputeLineHash(line), Times.Once);
        _fileUploadTrackingServiceMock.Verify(
            x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()),
            Times.Once);
        _parserServiceMock.Verify(x => x.ParseCnabLine(line, 0), Times.Once);
    }

    #endregion

    #region ProcessLineAsync - Duplicate Line Cases

    [Fact]
    public async Task ProcessLineAsync_WithDuplicateLine_ShouldReturnSkipped()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "duplicate-line-hash";

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Duplicate

        // Act
        var result = await _lineProcessor.ProcessLineAsync(
            line,
            0,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(LineProcessingResult.Skipped);
        _parserServiceMock.Verify(x => x.ParseCnabLine(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region ProcessLineAsync - Parse Failure Cases

    [Fact]
    public async Task ProcessLineAsync_WithParseFailure_ShouldReturnFailed()
    {
        // Arrange
        const string line = "invalid line";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _parserServiceMock
            .Setup(x => x.ParseCnabLine(line, 0))
            .Returns(Result<Transaction>.Failure("Invalid line format"));

        // Act
        var result = await _lineProcessor.ProcessLineAsync(
            line,
            0,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(LineProcessingResult.Failed);
        _transactionServiceMock.Verify(
            x => x.AddTransactionToContextAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ProcessLineAsync - Idempotency Cases

    [Fact]
    public async Task ProcessLineAsync_WithIdempotentDuplicate_ShouldReturnSkipped()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";

        var transaction = new Transaction
        {
            NatureCode = "3",
            Amount = 142.00m
        };

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _parserServiceMock
            .Setup(x => x.ParseCnabLine(line, 0))
            .Returns(Result<Transaction>.Success(transaction));

        _transactionServiceMock
            .Setup(x => x.AddTransactionToContextAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Failure("Duplicate transaction"));

        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<Transaction>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transaction already exists (idempotent)"));

        // Act
        var result = await _lineProcessor.ProcessLineAsync(
            line,
            0,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(LineProcessingResult.Skipped);
    }

    [Fact]
    public async Task ProcessLineAsync_WithUniqueConstraintViolation_ShouldReturnSkipped()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";

        var transaction = new Transaction
        {
            NatureCode = "3",
            Amount = 142.00m
        };

        var pgException = new PostgresException("Duplicate key", severity: string.Empty, invariantSeverity: string.Empty, sqlState: "23505");

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _parserServiceMock
            .Setup(x => x.ParseCnabLine(line, 0))
            .Returns(Result<Transaction>.Success(transaction));

        _transactionServiceMock
            .Setup(x => x.AddTransactionToContextAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Success(transaction));

        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<Transaction>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Unique constraint violation", pgException));

        // Act
        var result = await _lineProcessor.ProcessLineAsync(
            line,
            0,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(LineProcessingResult.Skipped);
    }

    #endregion

    #region ProcessLineAsync - Retry Cases

    [Fact]
    public async Task ProcessLineAsync_WithTransientFailure_ShouldRetry()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";

        var transaction = new Transaction
        {
            NatureCode = "3",
            Amount = 142.00m
        };

        var attemptCount = 0;

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _parserServiceMock
            .Setup(x => x.ParseCnabLine(line, 0))
            .Returns(Result<Transaction>.Success(transaction));

        _transactionServiceMock
            .Setup(x => x.AddTransactionToContextAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    return Result<Transaction>.Failure("Transient error");
                }
                return Result<Transaction>.Success(transaction);
            });

        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<Transaction>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<Task<Transaction>> operation, CancellationToken ct) =>
            {
                var result = operation().Result;
                if (attemptCount < 2)
                {
                    throw new Exception("Transient error");
                }
                return result;
            });

        // Act
        var result = await _lineProcessor.ProcessLineAsync(
            line,
            0,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(LineProcessingResult.Success);
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessLineAsync_WithAllRetriesFailing_ShouldReturnFailed()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";

        var transaction = new Transaction
        {
            NatureCode = "3",
            Amount = 142.00m
        };

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _parserServiceMock
            .Setup(x => x.ParseCnabLine(line, 0))
            .Returns(Result<Transaction>.Success(transaction));

        _transactionServiceMock
            .Setup(x => x.AddTransactionToContextAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Transaction>.Failure("Persistent error"));

        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<Transaction>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Persistent error"));

        // Act
        var result = await _lineProcessor.ProcessLineAsync(
            line,
            0,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(LineProcessingResult.Failed);
    }

    #endregion

    #region ProcessLineAsync - Cancellation Cases

    [Fact]
    public async Task ProcessLineAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = async () => await _lineProcessor.ProcessLineAsync(
            line,
            0,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region IdempotencyKey Generation Tests

    [Fact]
    public async Task ProcessLineAsync_ShouldGenerateCorrectIdempotencyKey()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";
        const string fileHash = "file-hash-123";
        var fileUploadId = Guid.NewGuid();
        const string lineHash = "line-hash-123";
        const int lineIndex = 5;

        var transaction = new Transaction
        {
            NatureCode = "3",
            Amount = 142.00m
        };

        Transaction? capturedTransaction = null;

        _hashServiceMock
            .Setup(x => x.ComputeLineHash(line))
            .Returns(lineHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsLineUniqueAsync(lineHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _parserServiceMock
            .Setup(x => x.ParseCnabLine(line, lineIndex))
            .Returns(Result<Transaction>.Success(transaction));

        _transactionServiceMock
            .Setup(x => x.AddTransactionToContextAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken ct) =>
            {
                capturedTransaction = t;
                return Result<Transaction>.Success(t);
            });

        _fileUploadTrackingServiceMock
            .Setup(x => x.RecordLineHashAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<Task<Transaction>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Transaction>>, CancellationToken>(async (operation, ct) => await operation());

        // Act
        await _lineProcessor.ProcessLineAsync(
            line,
            lineIndex,
            fileUploadId,
            fileHash,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _parserServiceMock.Object,
            _hashServiceMock.Object,
            _unitOfWorkMock.Object,
            maxRetries: 3,
            retryDelayMs: 10,
            _loggerMock.Object,
            CancellationToken.None);

        // Assert
        capturedTransaction.Should().NotBeNull();
        capturedTransaction!.IdempotencyKey.Should().Be($"{fileHash}:{lineIndex}");
        capturedTransaction.FileUploadId.Should().Be(fileUploadId);
    }

    #endregion
}

