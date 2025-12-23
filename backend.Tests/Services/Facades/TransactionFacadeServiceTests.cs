using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Facades;
using CnabApi.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace CnabApi.Tests.Services.Facades;

/// <summary>
/// Unit tests for TransactionFacadeService
/// </summary>
public class TransactionFacadeServiceTests
{
    private readonly Mock<ICnabUploadService> _uploadServiceMock;
    private readonly Mock<ITransactionService> _transactionServiceMock;
    private readonly Mock<IFileUploadService> _fileUploadServiceMock;
    private readonly Mock<IFileUploadTrackingService> _fileUploadTrackingServiceMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<ILogger<TransactionFacadeService>> _loggerMock;
    private readonly TransactionFacadeService _service;

    public TransactionFacadeServiceTests()
    {
        _uploadServiceMock = new Mock<ICnabUploadService>();
        _transactionServiceMock = new Mock<ITransactionService>();
        _fileUploadServiceMock = new Mock<IFileUploadService>();
        _fileUploadTrackingServiceMock = new Mock<IFileUploadTrackingService>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _loggerMock = new Mock<ILogger<TransactionFacadeService>>();

        _service = new TransactionFacadeService(
            _uploadServiceMock.Object,
            _transactionServiceMock.Object,
            _fileUploadServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _objectStorageServiceMock.Object,
            _loggerMock.Object
        );
    }

    #region UploadCnabFileAsync Tests

    [Fact]
    public async Task UploadCnabFileAsync_WithValidFile_ShouldReturnSuccess()
    {
        // Arrange
        var request = CreateMockHttpRequest();
        var fileContent = "Sample CNAB content";
        var transactionCount = 10;

        _fileUploadServiceMock
            .Setup(x => x.ReadCnabFileFromMultipartAsync(It.IsAny<Microsoft.AspNetCore.WebUtilities.MultipartReader>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(fileContent));

        _uploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(fileContent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(transactionCount));

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(transactionCount);
        result.Data.StatusCode.Should().Be(UploadStatusCode.Success);
    }

    [Fact]
    public async Task UploadCnabFileAsync_WithInvalidBoundary_ShouldReturnFailure()
    {
        // Arrange
        var request = CreateMockHttpRequest(contentType: "text/plain");

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid multipart/form-data request");
    }

    [Fact]
    public async Task UploadCnabFileAsync_WithFileValidationFailure_ShouldReturnFailure()
    {
        // Arrange
        var request = CreateMockHttpRequest();
        
        _fileUploadServiceMock
            .Setup(x => x.ReadCnabFileFromMultipartAsync(It.IsAny<Microsoft.AspNetCore.WebUtilities.MultipartReader>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("File is empty"));

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File is empty");
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task UploadCnabFileAsync_WithProcessingFailure_ShouldReturnFailure()
    {
        // Arrange
        var request = CreateMockHttpRequest();
        var fileContent = "Sample CNAB content";

        _fileUploadServiceMock
            .Setup(x => x.ReadCnabFileFromMultipartAsync(It.IsAny<Microsoft.AspNetCore.WebUtilities.MultipartReader>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(fileContent));

        _uploadServiceMock
            .Setup(x => x.ProcessCnabUploadAsync(fileContent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure("Invalid CNAB format"));

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid CNAB format");
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0);
        result.Data.StatusCode.Should().Be(UploadStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UploadCnabFileAsync_WithSizeError_ShouldReturnPayloadTooLarge()
    {
        // Arrange
        var request = CreateMockHttpRequest();
        
        _fileUploadServiceMock
            .Setup(x => x.ReadCnabFileFromMultipartAsync(It.IsAny<Microsoft.AspNetCore.WebUtilities.MultipartReader>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("File exceeds maximum size"));

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.PayloadTooLarge);
    }

    [Fact]
    public async Task UploadCnabFileAsync_WithUnsupportedType_ShouldReturnUnsupportedMediaType()
    {
        // Arrange
        var request = CreateMockHttpRequest();
        
        _fileUploadServiceMock
            .Setup(x => x.ReadCnabFileFromMultipartAsync(It.IsAny<Microsoft.AspNetCore.WebUtilities.MultipartReader>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("File type not allowed"));

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task UploadCnabFileAsync_WithEmptyError_ShouldReturnBadRequest()
    {
        // Arrange
        var request = CreateMockHttpRequest();
        
        _fileUploadServiceMock
            .Setup(x => x.ReadCnabFileFromMultipartAsync(It.IsAny<Microsoft.AspNetCore.WebUtilities.MultipartReader>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure("File is empty"));

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data!.StatusCode.Should().Be(UploadStatusCode.BadRequest);
    }

    #endregion

    #region GetTransactionsByCpfAsync Tests

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithValidCpf_ShouldReturnSuccess()
    {
        // Arrange
        var cpf = "12345678901";
        var transactions = new List<Transaction>
        {
            CreateTransaction(cpf, 100m),
            CreateTransaction(cpf, 200m)
        };
        var pagedResult = new PagedResult<Transaction>
        {
            Items = transactions,
            TotalCount = 2,
            Page = 1,
            PageSize = 50
        };

        _transactionServiceMock
            .Setup(x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Success(pagedResult));

        // Act
        var result = await _service.GetTransactionsByCpfAsync(cpf, 1, 50, null, null, null, "desc", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithDateFilter_ShouldPassCorrectOptions()
    {
        // Arrange
        var cpf = "12345678901";
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);
        TransactionQueryOptions? capturedOptions = null;

        _transactionServiceMock
            .Setup(x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionQueryOptions, CancellationToken>((opts, _) => capturedOptions = opts)
            .ReturnsAsync(Result<PagedResult<Transaction>>.Success(new PagedResult<Transaction> { Items = [], TotalCount = 0, Page = 1, PageSize = 50 }));

        // Act
        await _service.GetTransactionsByCpfAsync(cpf, 1, 50, startDate, endDate, null, "asc", CancellationToken.None);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Cpf.Should().Be(cpf);
        capturedOptions.StartDate.Should().Be(startDate);
        capturedOptions.EndDate.Should().Be(endDate);
        capturedOptions.SortDirection.Should().Be("asc");
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithTypeFilter_ShouldParseCorrectly()
    {
        // Arrange
        var cpf = "12345678901";
        var types = "1,2,3";
        TransactionQueryOptions? capturedOptions = null;

        _transactionServiceMock
            .Setup(x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionQueryOptions, CancellationToken>((opts, _) => capturedOptions = opts)
            .ReturnsAsync(Result<PagedResult<Transaction>>.Success(new PagedResult<Transaction> { Items = [], TotalCount = 0, Page = 1, PageSize = 50 }));

        // Act
        await _service.GetTransactionsByCpfAsync(cpf, 1, 50, null, null, types, "desc", CancellationToken.None);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.NatureCodes.Should().HaveCount(3);
        capturedOptions.NatureCodes.Should().Contain("1");
        capturedOptions.NatureCodes.Should().Contain("2");
        capturedOptions.NatureCodes.Should().Contain("3");
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithServiceFailure_ShouldReturnFailure()
    {
        // Arrange
        var cpf = "12345678901";
        
        _transactionServiceMock
            .Setup(x => x.GetTransactionsByCpfAsync(It.IsAny<TransactionQueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Failure("Database error"));

        // Act
        var result = await _service.GetTransactionsByCpfAsync(cpf, 1, 50, null, null, null, "desc", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Database error");
    }

    #endregion

    #region GetBalanceByCpfAsync Tests

    [Fact]
    public async Task GetBalanceByCpfAsync_WithValidCpf_ShouldReturnBalance()
    {
        // Arrange
        var cpf = "12345678901";
        var balance = 1500.50m;

        _transactionServiceMock
            .Setup(x => x.GetBalanceByCpfAsync(cpf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Success(balance));

        // Act
        var result = await _service.GetBalanceByCpfAsync(cpf, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(balance);
    }

    [Fact]
    public async Task GetBalanceByCpfAsync_WithServiceFailure_ShouldReturnFailure()
    {
        // Arrange
        var cpf = "12345678901";
        
        _transactionServiceMock
            .Setup(x => x.GetBalanceByCpfAsync(cpf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Failure("No transactions found"));

        // Act
        var result = await _service.GetBalanceByCpfAsync(cpf, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No transactions found");
    }

    #endregion

    #region SearchTransactionsByDescriptionAsync Tests

    [Fact]
    public async Task SearchTransactionsByDescriptionAsync_WithValidSearch_ShouldReturnResults()
    {
        // Arrange
        var cpf = "12345678901";
        var searchTerm = "restaurante";
        var transactions = new List<Transaction>
        {
            CreateTransaction(cpf, 100m, "Restaurante ABC"),
            CreateTransaction(cpf, 50m, "Restaurante XYZ")
        };
        var pagedResult = new PagedResult<Transaction>
        {
            Items = transactions,
            TotalCount = 2,
            Page = 1,
            PageSize = 50
        };

        _transactionServiceMock
            .Setup(x => x.SearchTransactionsByDescriptionAsync(cpf, searchTerm, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Success(pagedResult));

        // Act
        var result = await _service.SearchTransactionsByDescriptionAsync(cpf, searchTerm, 1, 50, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchTransactionsByDescriptionAsync_WithNoResults_ShouldReturnEmptyList()
    {
        // Arrange
        var cpf = "12345678901";
        var searchTerm = "nonexistent";
        var pagedResult = new PagedResult<Transaction>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 50
        };

        _transactionServiceMock
            .Setup(x => x.SearchTransactionsByDescriptionAsync(cpf, searchTerm, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Success(pagedResult));

        // Act
        var result = await _service.SearchTransactionsByDescriptionAsync(cpf, searchTerm, 1, 50, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTransactionsByDescriptionAsync_WithServiceFailure_ShouldReturnFailure()
    {
        // Arrange
        var cpf = "12345678901";
        var searchTerm = "test";
        
        _transactionServiceMock
            .Setup(x => x.SearchTransactionsByDescriptionAsync(cpf, searchTerm, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<Transaction>>.Failure("Search error"));

        // Act
        var result = await _service.SearchTransactionsByDescriptionAsync(cpf, searchTerm, 1, 50, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Search error");
    }

    #endregion

    #region ClearAllDataAsync Tests

    [Fact]
    public async Task ClearAllDataAsync_WhenSuccessful_ShouldReturnSuccess()
    {
        // Arrange
        _transactionServiceMock
            .Setup(x => x.ClearAllDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _service.ClearAllDataAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ClearAllDataAsync_WithServiceFailure_ShouldReturnFailure()
    {
        // Arrange
        _transactionServiceMock
            .Setup(x => x.ClearAllDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Database error"));

        // Act
        var result = await _service.ClearAllDataAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Database error");
    }

    #endregion

    #region Helper Methods

    private static HttpRequest CreateMockHttpRequest(string contentType = "multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW")
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        return context.Request;
    }

    private static Transaction CreateTransaction(string cpf, decimal value, string storeName = "Test Store")
    {
        return new Transaction
        {
            Id = 1,
            Cpf = cpf,
            Amount = value,
            StoreName = storeName,
            StoreOwner = "Test Owner",
            TransactionDate = DateTime.Now,
            TransactionTime = DateTime.Now.TimeOfDay,
            NatureCode = "1",
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
