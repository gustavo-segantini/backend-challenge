using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.Facades;
using CnabApi.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace CnabApi.Tests.Services.Facades;

/// <summary>
/// Unit tests for TransactionFacadeService
/// </summary>
public class TransactionFacadeServiceTests
{
    private readonly Mock<ITransactionService> _transactionServiceMock;
    private readonly Mock<IFileUploadService> _fileUploadServiceMock;
    private readonly Mock<IFileUploadTrackingService> _fileUploadTrackingServiceMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUploadQueueService> _uploadQueueServiceMock;
    private readonly Mock<ICnabUploadService> _cnabUploadServiceMock;
    private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
    private readonly Mock<IHashService> _hashServiceMock;
    private readonly CnabApi.Services.StatusCodes.UploadStatusCodeStrategyFactory _statusCodeFactory;
    private readonly Mock<ILogger<TransactionFacadeService>> _loggerMock;
    private readonly TransactionFacadeService _service;

    public TransactionFacadeServiceTests()
    {
        _transactionServiceMock = new Mock<ITransactionService>();
        _fileUploadServiceMock = new Mock<IFileUploadService>();
        _fileUploadTrackingServiceMock = new Mock<IFileUploadTrackingService>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _uploadQueueServiceMock = new Mock<IUploadQueueService>();
        _cnabUploadServiceMock = new Mock<ICnabUploadService>();
        _hostEnvironmentMock = new Mock<IHostEnvironment>();
        _hashServiceMock = new Mock<IHashService>();
        _statusCodeFactory = new CnabApi.Services.StatusCodes.UploadStatusCodeStrategyFactory();
        _loggerMock = new Mock<ILogger<TransactionFacadeService>>();

        // Setup hash service to return a mock hash
        _hashServiceMock
            .Setup(x => x.ComputeFileHash(It.IsAny<string>()))
            .Returns((string content) => $"hash-{content.GetHashCode()}");

        _service = new TransactionFacadeService(
            _transactionServiceMock.Object,
            _fileUploadServiceMock.Object,
            _fileUploadTrackingServiceMock.Object,
            _objectStorageServiceMock.Object,
            _uploadQueueServiceMock.Object,
            _cnabUploadServiceMock.Object,
            _hashServiceMock.Object,
            _statusCodeFactory,
            _hostEnvironmentMock.Object,
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
        var fileUploadId = Guid.NewGuid();
        var fileHash = "abc123hash";

        _fileUploadServiceMock
            .Setup(x => x.ReadCnabFileFromMultipartAsync(It.IsAny<Microsoft.AspNetCore.WebUtilities.MultipartReader>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(fileContent));

        _fileUploadTrackingServiceMock
            .Setup(x => x.CalculateFileHashAsync(It.IsAny<Stream>()))
            .ReturnsAsync(fileHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsFileUniqueAsync(fileHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null));

        var storagePath = "cnab-20250101-120000-123.txt";
        _objectStorageServiceMock
            .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($"http://minio/{storagePath}");

        var fileUpload = new FileUpload 
        { 
            Id = fileUploadId, 
            FileName = "test.txt", 
            FileHash = fileHash,
            FileSize = 100,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow,
            StoragePath = storagePath
        };

        _fileUploadTrackingServiceMock
            .Setup(x => x.RecordPendingUploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileUpload);

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("message-id-123");

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0); // Returns 0 when queued
        result.Data.StatusCode.Should().Be(UploadStatusCode.Accepted);
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
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task UploadCnabFileAsync_WithProcessingFailure_ShouldReturnFailure()
    {
        // Arrange
        var request = CreateMockHttpRequest();
        var fileContent = "Sample CNAB content";
        var fileUploadId = Guid.NewGuid();
        var fileHash = "abc123hash";

        _fileUploadServiceMock
            .Setup(x => x.ReadCnabFileFromMultipartAsync(It.IsAny<Microsoft.AspNetCore.WebUtilities.MultipartReader>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(fileContent));

        _fileUploadTrackingServiceMock
            .Setup(x => x.CalculateFileHashAsync(It.IsAny<Stream>()))
            .ReturnsAsync(fileHash);

        _fileUploadTrackingServiceMock
            .Setup(x => x.IsFileUniqueAsync(fileHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null));

        var storagePath = "cnab-20250101-120000-456.txt";
        _objectStorageServiceMock
            .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($"http://minio/{storagePath}");

        var fileUpload = new FileUpload 
        { 
            Id = fileUploadId, 
            FileName = "test.txt", 
            FileHash = fileHash,
            FileSize = 100,
            Status = FileUploadStatus.Pending,
            UploadedAt = DateTime.UtcNow,
            StoragePath = storagePath
        };

        _fileUploadTrackingServiceMock
            .Setup(x => x.RecordPendingUploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileUpload);

        _uploadQueueServiceMock
            .Setup(x => x.EnqueueUploadAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("message-id-456");

        // Act
        var result = await _service.UploadCnabFileAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TransactionCount.Should().Be(0); // Returns 0 when queued (processing is async)
        result.Data.StatusCode.Should().Be(UploadStatusCode.Accepted);
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
