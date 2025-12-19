using CnabApi.Common;
using CnabApi.Controllers;
using CnabApi.Models;
using CnabApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CnabApi.Tests.Controllers;

/// <summary>
/// Unit tests for the TransactionsController.
/// Tests all API endpoints for CNAB file upload and transaction management.
/// </summary>
public class TransactionsControllerTests
{
    private readonly Mock<ICnabUploadService> _uploadServiceMock;
    private readonly Mock<ITransactionService> _transactionServiceMock;
    private readonly Mock<ILogger<TransactionsController>> _loggerMock;
    private readonly TransactionsController _controller;

    public TransactionsControllerTests()
    {
        _uploadServiceMock = new Mock<ICnabUploadService>();
        _transactionServiceMock = new Mock<ITransactionService>();
        _loggerMock = new Mock<ILogger<TransactionsController>>();

        _controller = new TransactionsController(
            _uploadServiceMock.Object,
            _transactionServiceMock.Object,
            _loggerMock.Object
        );
    }

    #region UploadCnabFile Tests

    [Fact]
    public async Task UploadCnabFile_WithValidFile_ShouldReturnOkWithTransactionCount()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "valid content");
        _uploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(5));

        // Act
        var result = await _controller.UploadCnabFile(file, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        value.Should().NotBeNull();
        
        // Check the anonymous object properties
        var messageProperty = value!.GetType().GetProperty("message");
        var countProperty = value.GetType().GetProperty("count");
        
        messageProperty!.GetValue(value).Should().Be("Successfully imported 5 transactions");
        countProperty!.GetValue(value).Should().Be(5);
    }

    [Fact]
    public async Task UploadCnabFile_WithInvalidFile_ShouldReturnBadRequest()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "invalid content");
        _uploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure("Arquivo inválido"));

        // Act
        var result = await _controller.UploadCnabFile(file, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        value.Should().NotBeNull();
        
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("Arquivo inválido");
    }

    [Fact]
    public async Task UploadCnabFile_ShouldCallUploadService()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "content");
        _uploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(1));

        // Act
        await _controller.UploadCnabFile(file, CancellationToken.None);

        // Assert
        _uploadServiceMock.Verify(
            x => x.ProcessCnabUploadAsync(file, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task UploadCnabFile_WithZeroTransactions_ShouldReturnOkWithZeroCount()
    {
        // Arrange
        var file = CreateMockFormFile("empty.txt", "");
        _uploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));

        // Act
        var result = await _controller.UploadCnabFile(file, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var countProperty = value!.GetType().GetProperty("count");
        countProperty!.GetValue(value).Should().Be(0);
    }

    #endregion

    #region GetTransactionsByCpf Tests

    [Fact]
    public async Task GetTransactionsByCpf_WithValidCpf_ShouldReturnOkWithTransactions()
    {
        // Arrange
        var cpf = "12345678901";
        var transactions = new List<Transaction>
        {
            CreateTransaction("1", 100m, cpf),
            CreateTransaction("2", 50m, cpf)
        };

        _transactionServiceMock
            .Setup(x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Success(CreatePaged(transactions, 2, 1, 50)));

        // Act
        var result = await _controller.GetTransactionsByCpf(cpf, 1, 50, null, null, null, "desc", CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<PagedResult<Transaction>>().Subject;
        returned.Items.Should().HaveCount(2);
        returned.TotalCount.Should().Be(2);
        returned.Items.Should().AllSatisfy(t => t.Cpf.Should().Be(cpf));
    }

    [Fact]
    public async Task GetTransactionsByCpf_WithNonExistingCpf_ShouldReturnEmptyList()
    {
        // Arrange
        var cpf = "99999999999";
        _transactionServiceMock
            .Setup(x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Success(CreatePaged(new List<Transaction>(), 0, 1, 50)));

        // Act
        var result = await _controller.GetTransactionsByCpf(cpf, 1, 50, null, null, null, "desc", CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<PagedResult<Transaction>>().Subject;
        returned.Items.Should().BeEmpty();
        returned.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTransactionsByCpf_WithInvalidCpf_ShouldReturnBadRequest()
    {
        // Arrange
        var cpf = "";
        _transactionServiceMock
            .Setup(x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Failure("CPF is required."));

        // Act
        var result = await _controller.GetTransactionsByCpf(cpf, 1, 50, null, null, null, "desc", CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("CPF is required."); 
    }

    [Fact]
    public async Task GetTransactionsByCpf_ShouldCallTransactionService()
    {
        // Arrange
        var cpf = "12345678901";
        _transactionServiceMock
            .Setup(x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Success(CreatePaged(new List<Transaction>(), 0, 1, 50)));

        // Act
        await _controller.GetTransactionsByCpf(cpf, 1, 50, null, null, null, "desc", CancellationToken.None);

        // Assert
        _transactionServiceMock.Verify(
            x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    #endregion

    #region GetBalance Tests

    [Fact]
    public async Task GetBalance_WithValidCpf_ShouldReturnOkWithBalance()
    {
        // Arrange
        var cpf = "12345678901";
        var expectedBalance = 1500.50m;
        
        _transactionServiceMock
            .Setup(x => x.GetBalanceByCpfAsync(cpf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Success(expectedBalance));

        // Act
        var result = await _controller.GetBalance(cpf, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var balanceProperty = value!.GetType().GetProperty("balance");
        balanceProperty!.GetValue(value).Should().Be(expectedBalance);
    }

    [Fact]
    public async Task GetBalance_WithNoTransactions_ShouldReturnZeroBalance()
    {
        // Arrange
        var cpf = "12345678901";
        
        _transactionServiceMock
            .Setup(x => x.GetBalanceByCpfAsync(cpf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Success(0m));

        // Act
        var result = await _controller.GetBalance(cpf, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var balanceProperty = value!.GetType().GetProperty("balance");
        balanceProperty!.GetValue(value).Should().Be(0m);
    }

    [Fact]
    public async Task GetBalance_WithNegativeBalance_ShouldReturnNegativeValue()
    {
        // Arrange
        var cpf = "12345678901";
        var negativeBalance = -500.00m;
        
        _transactionServiceMock
            .Setup(x => x.GetBalanceByCpfAsync(cpf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Success(negativeBalance));

        // Act
        var result = await _controller.GetBalance(cpf, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var balanceProperty = value!.GetType().GetProperty("balance");
        balanceProperty!.GetValue(value).Should().Be(negativeBalance);
    }

    [Fact]
    public async Task GetBalance_WithInvalidCpf_ShouldReturnBadRequest()
    {
        // Arrange
        var cpf = "";
        _transactionServiceMock
            .Setup(x => x.GetBalanceByCpfAsync(cpf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Failure("CPF is required."));

        // Act
        var result = await _controller.GetBalance(cpf, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("CPF is required.");
    }

    [Fact]
    public async Task GetBalance_ShouldCallTransactionService()
    {
        // Arrange
        var cpf = "12345678901";
        _transactionServiceMock
            .Setup(x => x.GetBalanceByCpfAsync(cpf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Success(100m));

        // Act
        await _controller.GetBalance(cpf, CancellationToken.None);

        // Assert
        _transactionServiceMock.Verify(
            x => x.GetBalanceByCpfAsync(cpf, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    #endregion

    #region ClearData Tests

    [Fact]
    public async Task ClearData_WhenSuccessful_ShouldReturnOkWithMessage()
    {
        // Arrange
        _transactionServiceMock
            .Setup(x => x.ClearAllDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _controller.ClearData(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var messageProperty = value!.GetType().GetProperty("message");
        messageProperty!.GetValue(value).Should().Be("All data cleared successfully");
    }

    [Fact]
    public async Task ClearData_WhenFails_ShouldReturnBadRequest()
    {
        // Arrange
        _transactionServiceMock
            .Setup(x => x.ClearAllDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Erro ao limpar dados"));

        // Act
        var result = await _controller.ClearData(CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("Erro ao limpar dados");
    }

    [Fact]
    public async Task ClearData_ShouldCallTransactionService()
    {
        // Arrange
        _transactionServiceMock
            .Setup(x => x.ClearAllDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        await _controller.ClearData(CancellationToken.None);

        // Assert
        _transactionServiceMock.Verify(
            x => x.ClearAllDataAsync(It.IsAny<CancellationToken>()), 
            Times.Once);
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

    private static Transaction CreateTransaction(string natureCode, decimal amount, string cpf)
    {
        return new Transaction
        {
            NatureCode = natureCode,
            Amount = amount,
            Cpf = cpf,
            Card = "1234****5678",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = new TimeSpan(12, 0, 0),
            BankCode = natureCode
        };
    }

    private static PagedResult<Transaction> CreatePaged(List<Transaction> items, int total, int page, int pageSize)
    {
        return new PagedResult<Transaction>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion
}
