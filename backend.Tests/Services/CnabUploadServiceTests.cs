using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Interfaces;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for the CnabUploadService.
/// Tests the orchestration of file upload workflow.
/// </summary>
public class CnabUploadServiceTests
{
    private readonly Mock<ICnabParserService> _parserServiceMock;
    private readonly Mock<ITransactionService> _transactionServiceMock;
    private readonly Mock<IFileUploadTrackingService> _fileUploadTrackingServiceMock;
    private readonly Mock<ILogger<CnabUploadService>> _loggerMock;
    private readonly CnabUploadService _uploadService;

    public CnabUploadServiceTests()
    {
        _parserServiceMock = new Mock<ICnabParserService>();
        _transactionServiceMock = new Mock<ITransactionService>();
        _fileUploadTrackingServiceMock = new Mock<IFileUploadTrackingService>();
        _loggerMock = new Mock<ILogger<CnabUploadService>>();

        _uploadService = new CnabUploadService(
            _parserServiceMock.Object,
            _transactionServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _loggerMock.Object
        );
    }

    #region ProcessCnabUploadAsync - Success Cases

    [Fact]
    public async Task ProcessCnabUploadAsync_WithValidFileContent_ShouldReturnSuccess()
    {
        // Arrange
        var fileContent = "valid CNAB content";
        var transactions = new List<Transaction>
        {
            CreateTransaction("1", 100m),
            CreateTransaction("2", 50m)
        };

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(fileContent))
            .Returns(Result<List<Transaction>>.Success(transactions));

        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<Transaction>>.Success(transactions));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(2);
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_ShouldCallServicesInCorrectOrder()
    {
        // Arrange
        var fileContent = "content";
        var callOrder = new List<string>();
        var transactions = new List<Transaction> { CreateTransaction("1", 100m) };

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(It.IsAny<string>()))
            .Callback(() => callOrder.Add("ParseCnabFile"))
            .Returns(Result<List<Transaction>>.Success(transactions));

        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("AddTransactionsAsync"))
            .ReturnsAsync(Result<List<Transaction>>.Success(transactions));

        // Act
        await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        callOrder.Should().ContainInOrder("ParseCnabFile", "AddTransactionsAsync");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_ShouldPassParsedTransactionsToTransactionService()
    {
        // Arrange
        var fileContent = "content";
        var expectedTransactions = new List<Transaction>
        {
            CreateTransaction("1", 100m),
            CreateTransaction("2", 200m),
            CreateTransaction("3", 300m)
        };

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(fileContent))
            .Returns(Result<List<Transaction>>.Success(expectedTransactions));

        List<Transaction>? capturedTransactions = null;
        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Transaction>, CancellationToken>((t, _) => capturedTransactions = t)
            .ReturnsAsync(Result<List<Transaction>>.Success(expectedTransactions));

        // Act
        await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        capturedTransactions.Should().NotBeNull();
        capturedTransactions.Should().HaveCount(3);
        capturedTransactions.Should().HaveCount(3);
    }

    #endregion

    #region ProcessCnabUploadAsync - Failure Cases

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenContentIsEmpty_ShouldReturnFailure()
    {
        // Arrange
        var fileContent = "";

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not provided or is empty");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenContentIsNull_ShouldReturnFailure()
    {
        // Arrange
        string fileContent = null!;

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not provided or is empty");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenParsingFails_ShouldReturnFailure()
    {
        // Arrange
        var fileContent = "invalid content";

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(fileContent))
            .Returns(Result<List<Transaction>>.Failure("Invalid CNAB format"));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid CNAB format");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenParsingFails_ShouldNotCallTransactionService()
    {
        // Arrange
        var fileContent = "content";

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(fileContent))
            .Returns(Result<List<Transaction>>.Failure("Error"));

        // Act
        await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        _transactionServiceMock.Verify(
            x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenSavingFails_ShouldReturnFailure()
    {
        // Arrange
        var fileContent = "content";
        var transactions = new List<Transaction> { CreateTransaction("1", 100m) };

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(fileContent))
            .Returns(Result<List<Transaction>>.Success(transactions));

        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<Transaction>>.Failure("Database error"));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenTransactionServiceThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var fileContent = "content";
        var transactions = new List<Transaction> { CreateTransaction("1", 100m) };

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(fileContent))
            .Returns(Result<List<Transaction>>.Success(transactions));

        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(fileContent);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static Transaction CreateTransaction(string natureCode, decimal amount)
    {
        return new Transaction
        {
            NatureCode = natureCode,
            Amount = amount,
            Cpf = "12345678901",
            Card = "1234****5678",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = new TimeSpan(12, 0, 0),
            BankCode = natureCode
        };
    }

    #endregion
}
