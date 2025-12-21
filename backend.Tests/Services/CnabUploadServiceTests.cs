using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for the CnabUploadService.
/// Tests the orchestration of file upload workflow.
/// </summary>
public class CnabUploadServiceTests
{
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly Mock<ICnabParserService> _parserServiceMock;
    private readonly Mock<ITransactionService> _transactionServiceMock;
    private readonly Mock<ILogger<CnabUploadService>> _loggerMock;
    private readonly CnabUploadService _uploadService;

    public CnabUploadServiceTests()
    {
        _fileServiceMock = new Mock<IFileService>();
        _parserServiceMock = new Mock<ICnabParserService>();
        _transactionServiceMock = new Mock<ITransactionService>();
        _loggerMock = new Mock<ILogger<CnabUploadService>>();

        _uploadService = new CnabUploadService(
            _fileServiceMock.Object,
            _parserServiceMock.Object,
            _transactionServiceMock.Object,
            _loggerMock.Object
        );
    }

    #region ProcessCnabUploadAsync - Success Cases

    [Fact]
    public async Task ProcessCnabUploadAsync_WithValidFile_ShouldReturnSuccess()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "valid content");
        var fileContent = "valid CNAB content";
        var transactions = new List<Transaction>
        {
            CreateTransaction("1", 100m),
            CreateTransaction("2", 50m)
        };

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(fileContent));

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(fileContent))
            .Returns(Result<List<Transaction>>.Success(transactions));

        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(transactions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<Transaction>>.Success(transactions));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(2);
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_ShouldCallServicesInCorrectOrder()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");
        var callOrder = new List<string>();
        var transactions = new List<Transaction> { CreateTransaction("1", 100m) };

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("ReadCnabFileAsync"))
            .ReturnsAsync(Result<string>.Success("content"));

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(It.IsAny<string>()))
            .Callback(() => callOrder.Add("ParseCnabFile"))
            .Returns(Result<List<Transaction>>.Success(transactions));

        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("AddTransactionsAsync"))
            .ReturnsAsync(Result<List<Transaction>>.Success(transactions));

        // Act
        await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        callOrder.Should().ContainInOrder("ReadCnabFileAsync", "ParseCnabFile", "AddTransactionsAsync");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_ShouldPassParsedTransactionsToTransactionService()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");
        var expectedTransactions = new List<Transaction>
        {
            CreateTransaction("1", 100m),
            CreateTransaction("2", 200m),
            CreateTransaction("3", 300m)
        };

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("content"));

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(It.IsAny<string>()))
            .Returns(Result<List<Transaction>>.Success(expectedTransactions));

        List<Transaction>? capturedTransactions = null;
        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Transaction>, CancellationToken>((t, _) => capturedTransactions = t)
            .ReturnsAsync(Result<List<Transaction>>.Success(expectedTransactions));

        // Act
        await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        capturedTransactions.Should().NotBeNull();
        capturedTransactions.Should().HaveCount(3);
        capturedTransactions.Should().BeEquivalentTo(expectedTransactions);
    }

    #endregion

    #region ProcessCnabUploadAsync - Failure Cases

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenFileReadingFails_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("Arquivo inválido"));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("inválido");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenFileReadingFails_ShouldNotCallParser()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("Error"));

        // Act
        await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        _parserServiceMock.Verify(x => x.ParseCnabFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenParsingFails_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("content"));

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(It.IsAny<string>()))
            .Returns(Result<List<Transaction>>.Failure("Erro ao parsear CNAB"));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("parsear");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenParsingFails_ShouldNotCallTransactionService()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("content"));

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(It.IsAny<string>()))
            .Returns(Result<List<Transaction>>.Failure("Error"));

        // Act
        await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        _transactionServiceMock.Verify(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenSavingFails_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");
        var transactions = new List<Transaction> { CreateTransaction("1", 100m) };

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("content"));

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(It.IsAny<string>()))
            .Returns(Result<List<Transaction>>.Success(transactions));

        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<Transaction>>.Failure("Erro ao salvar no banco"));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("salvar");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenFileServiceThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Simulated file read exception"));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unexpected error");
    }

    [Fact]
    public async Task ProcessCnabUploadAsync_WhenTransactionServiceThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");
        var transactions = new List<Transaction> { CreateTransaction("1", 100m) };

        _fileServiceMock
            .Setup(x => x.ReadCnabFileAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("content"));

        _parserServiceMock
            .Setup(x => x.ParseCnabFile(It.IsAny<string>()))
            .Returns(Result<List<Transaction>>.Success(transactions));

        _transactionServiceMock
            .Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated database exception"));

        // Act
        var result = await _uploadService.ProcessCnabUploadAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unexpected error");
    }

    #endregion

    #region Helper Methods

    private static IFormFile CreateMockFormFile(string fileName, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
    }

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
