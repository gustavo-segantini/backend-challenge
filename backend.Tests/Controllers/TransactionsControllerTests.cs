using CnabApi.Common;
using CnabApi.Controllers;
using CnabApi.Models;
using CnabApi.Models.Responses;
using CnabApi.Services;
using CnabApi.Services.Facades;
using CnabApi.Services.Interfaces;
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
    private readonly Mock<ITransactionFacadeService> _facadeServiceMock;
    private readonly Mock<IUploadManagementService> _uploadManagementServiceMock;
    private readonly Mock<ILogger<TransactionsController>> _loggerMock;
    private readonly TransactionsController _controller;

    public TransactionsControllerTests()
    {
        _facadeServiceMock = new Mock<ITransactionFacadeService>();
        _uploadManagementServiceMock = new Mock<IUploadManagementService>();
        _loggerMock = new Mock<ILogger<TransactionsController>>();

        _controller = new TransactionsController(
            _facadeServiceMock.Object,
            _uploadManagementServiceMock.Object,
            _loggerMock.Object
        );
    }

    #region UploadCnabFile Tests

    [Fact]
    public async Task UploadCnabFile_WhenSuccessful_ShouldReturnOkWithTransactionCount()
    {
        // Arrange
        SetupControllerHttpContext();
        var uploadResult = new UploadResult
        {
            TransactionCount = 201,
            StatusCode = UploadStatusCode.Success
        };

        _facadeServiceMock
            .Setup(x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadResult>.Success(uploadResult));

        // Act
        var result = await _controller.UploadCnabFile(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        var messageProperty = value!.GetType().GetProperty("message");
        var countProperty = value.GetType().GetProperty("count");

        messageProperty!.GetValue(value).Should().Be("Successfully imported 201 transactions");
        countProperty!.GetValue(value).Should().Be(201);
    }

    [Fact]
    public async Task UploadCnabFile_WhenAccepted_ShouldReturnAccepted()
    {
        // Arrange
        SetupControllerHttpContext();
        var uploadResult = new UploadResult
        {
            TransactionCount = 0,
            StatusCode = UploadStatusCode.Accepted,
            UploadId = Guid.NewGuid()
        };

        _facadeServiceMock
            .Setup(x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadResult>.Success(uploadResult));

        // Act
        var result = await _controller.UploadCnabFile(CancellationToken.None);

        // Assert
        var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
        var value = acceptedResult.Value;
        var messageProperty = value!.GetType().GetProperty("message");
        var statusProperty = value.GetType().GetProperty("status");

        messageProperty!.GetValue(value).Should().Be("File accepted and queued for background processing");
        statusProperty!.GetValue(value).Should().Be("processing");
    }

    [Fact]
    public async Task UploadCnabFile_WhenBadRequest_ShouldReturnBadRequest()
    {
        // Arrange
        SetupControllerHttpContext();
        var uploadResult = new UploadResult
        {
            StatusCode = UploadStatusCode.BadRequest
        };

        _facadeServiceMock
            .Setup(x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadResult>.Failure("File is empty", uploadResult));

        // Act
        var result = await _controller.UploadCnabFile(CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("File is empty");
    }

    [Fact]
    public async Task UploadCnabFile_WhenUnsupportedMediaType_ShouldReturnUnsupportedMediaType()
    {
        // Arrange
        SetupControllerHttpContext();
        var uploadResult = new UploadResult
        {
            StatusCode = UploadStatusCode.UnsupportedMediaType
        };

        _facadeServiceMock
            .Setup(x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadResult>.Failure("Unsupported media type", uploadResult));

        // Act
        var result = await _controller.UploadCnabFile(CancellationToken.None);

        // Assert
        var unsupportedMediaTypeResult = result.Should().BeOfType<ObjectResult>().Subject;
        unsupportedMediaTypeResult.StatusCode.Should().Be(415);
        var value = unsupportedMediaTypeResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("Unsupported media type");
    }

    [Fact]
    public async Task UploadCnabFile_WhenPayloadTooLarge_ShouldReturnPayloadTooLarge()
    {
        // Arrange
        SetupControllerHttpContext();
        var uploadResult = new UploadResult
        {
            StatusCode = UploadStatusCode.PayloadTooLarge
        };

        _facadeServiceMock
            .Setup(x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadResult>.Failure("File too large", uploadResult));

        // Act
        var result = await _controller.UploadCnabFile(CancellationToken.None);

        // Assert
        var payloadTooLargeResult = result.Should().BeOfType<ObjectResult>().Subject;
        payloadTooLargeResult.StatusCode.Should().Be(413);
        var value = payloadTooLargeResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("File too large");
    }

    [Fact]
    public async Task UploadCnabFile_WhenUnprocessableEntity_ShouldReturnUnprocessableEntity()
    {
        // Arrange
        SetupControllerHttpContext();
        var uploadResult = new UploadResult
        {
            StatusCode = UploadStatusCode.UnprocessableEntity
        };

        _facadeServiceMock
            .Setup(x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadResult>.Failure("Invalid CNAB format", uploadResult));

        // Act
        var result = await _controller.UploadCnabFile(CancellationToken.None);

        // Assert
        var unprocessableEntityResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        unprocessableEntityResult.StatusCode.Should().Be(422);
        unprocessableEntityResult.Value.Should().NotBeNull();
        
        // Verify the error message is in the response (serialized as JSON)
        var valueString = System.Text.Json.JsonSerializer.Serialize(unprocessableEntityResult.Value);
        valueString.Should().Contain("Invalid CNAB format");
    }

    [Fact]
    public async Task UploadCnabFile_WhenFailureWithoutStatusCode_ShouldReturnBadRequest()
    {
        // Arrange
        SetupControllerHttpContext();

        _facadeServiceMock
            .Setup(x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadResult>.Failure("Unknown error"));

        // Act
        var result = await _controller.UploadCnabFile(CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("Unknown error");
    }

    [Fact]
    public async Task UploadCnabFile_ShouldCallFacadeService()
    {
        // Arrange
        SetupControllerHttpContext();
        var uploadResult = new UploadResult
        {
            TransactionCount = 100,
            StatusCode = UploadStatusCode.Success
        };

        _facadeServiceMock
            .Setup(x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UploadResult>.Success(uploadResult));

        // Act
        await _controller.UploadCnabFile(CancellationToken.None);

        // Assert
        _facadeServiceMock.Verify(
            x => x.UploadCnabFileAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion


    #region ClearData Tests

    [Fact]
    public async Task ClearData_WhenSuccessful_ShouldReturnOkWithMessage()
    {
        // Arrange
        _facadeServiceMock
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
        _facadeServiceMock
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
        _facadeServiceMock
            .Setup(x => x.ClearAllDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        await _controller.ClearData(CancellationToken.None);

        // Assert
        _facadeServiceMock.Verify(
            x => x.ClearAllDataAsync(It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    #endregion

    #region GetAllUploads Tests

    [Fact]
    public async Task GetAllUploads_WhenSuccessful_ShouldReturnOkWithPagedResults()
    {
        // Arrange
        var pagedResponse = new PagedResponse<FileUploadResponse>
        {
            Items = new List<FileUploadResponse>
            {
                CreateFileUpload(Guid.NewGuid(), "file1.txt", FileUploadStatus.Success, 100, 100, 0, 0).ToResponse(),
                CreateFileUpload(Guid.NewGuid(), "file2.txt", FileUploadStatus.Processing, 200, 50, 0, 0).ToResponse()
            },
            TotalCount = 2,
            Page = 1,
            PageSize = 50,
            TotalPages = 1
        };

        _uploadManagementServiceMock
            .Setup(x => x.GetAllUploadsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FileUploadStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResponse);

        // Act
        var result = await _controller.GetAllUploads(1, 50, null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<PagedResponse<FileUploadResponse>>().Subject;

        value.Items.Should().HaveCount(2);
        value.TotalCount.Should().Be(2);
        value.Page.Should().Be(1);
        value.PageSize.Should().Be(50);
        value.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetAllUploads_WithStatusFilter_ShouldCallServiceWithFilter()
    {
        // Arrange
        var pagedResponse = new PagedResponse<FileUploadResponse>
        {
            Items = new List<FileUploadResponse>
            {
                CreateFileUpload(Guid.NewGuid(), "file1.txt", FileUploadStatus.Success, 100, 100, 0, 0).ToResponse()
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 50,
            TotalPages = 1
        };

        _uploadManagementServiceMock
            .Setup(x => x.GetAllUploadsAsync(It.IsAny<int>(), It.IsAny<int>(), FileUploadStatus.Success, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResponse);

        // Act
        await _controller.GetAllUploads(1, 50, "Success", CancellationToken.None);

        // Assert
        _uploadManagementServiceMock.Verify(
            x => x.GetAllUploadsAsync(1, 50, FileUploadStatus.Success, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUploads_WithInvalidStatus_ShouldIgnoreFilter()
    {
        // Arrange
        var pagedResponse = new PagedResponse<FileUploadResponse>
        {
            Items = new List<FileUploadResponse>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50,
            TotalPages = 0
        };

        _uploadManagementServiceMock
            .Setup(x => x.GetAllUploadsAsync(It.IsAny<int>(), It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResponse);

        // Act
        await _controller.GetAllUploads(1, 50, "InvalidStatus", CancellationToken.None);

        // Assert
        _uploadManagementServiceMock.Verify(
            x => x.GetAllUploadsAsync(1, 50, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllUploads_ShouldCalculateProgressPercentage()
    {
        // Arrange
        var upload = CreateFileUpload(Guid.NewGuid(), "file1.txt", FileUploadStatus.Processing, 100, 50, 10, 5);
        var pagedResponse = new PagedResponse<FileUploadResponse>
        {
            Items = new List<FileUploadResponse> { upload.ToResponse() },
            TotalCount = 1,
            Page = 1,
            PageSize = 50,
            TotalPages = 1
        };

        _uploadManagementServiceMock
            .Setup(x => x.GetAllUploadsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<FileUploadStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResponse);

        // Act
        var result = await _controller.GetAllUploads(1, 50, null, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<PagedResponse<FileUploadResponse>>().Subject;
        var firstItem = value.Items.First();
        
        // (50 + 10 + 5) / 100 * 100 = 65%
        firstItem.ProgressPercentage.Should().Be(65.0);
    }

    #endregion

    #region GetIncompleteUploads Tests

    [Fact]
    public async Task GetIncompleteUploads_WhenSuccessful_ShouldReturnOkWithIncompleteUploads()
    {
        // Arrange
        var incompleteResponse = new IncompleteUploadsResponse
        {
            IncompleteUploads = new List<FileUploadResponse>
            {
                CreateFileUpload(Guid.NewGuid(), "file1.txt", FileUploadStatus.Processing, 100, 50, 0, 0).ToResponse()
            },
            Count = 1
        };

        _uploadManagementServiceMock
            .Setup(x => x.GetIncompleteUploadsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteResponse);

        // Act
        var result = await _controller.GetIncompleteUploads(30, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<IncompleteUploadsResponse>().Subject;

        value.IncompleteUploads.Should().HaveCount(1);
        value.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetIncompleteUploads_WhenNoIncompleteUploads_ShouldReturnEmptyList()
    {
        // Arrange
        var incompleteResponse = new IncompleteUploadsResponse
        {
            IncompleteUploads = new List<FileUploadResponse>(),
            Count = 0
        };

        _uploadManagementServiceMock
            .Setup(x => x.GetIncompleteUploadsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteResponse);

        // Act
        var result = await _controller.GetIncompleteUploads(30, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<IncompleteUploadsResponse>().Subject;
        
        value.Count.Should().Be(0);
        value.IncompleteUploads.Should().BeEmpty();
    }

    [Fact]
    public async Task GetIncompleteUploads_ShouldCallServiceWithTimeoutMinutes()
    {
        // Arrange
        var incompleteResponse = new IncompleteUploadsResponse
        {
            IncompleteUploads = new List<FileUploadResponse>(),
            Count = 0
        };

        _uploadManagementServiceMock
            .Setup(x => x.GetIncompleteUploadsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteResponse);

        // Act
        await _controller.GetIncompleteUploads(60, CancellationToken.None);

        // Assert
        _uploadManagementServiceMock.Verify(
            x => x.GetIncompleteUploadsAsync(60, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ResumeUpload Tests

    [Fact]
    public async Task ResumeUpload_WhenUploadNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _uploadManagementServiceMock
            .Setup(x => x.ResumeUploadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ResumeUploadResponse>.Failure("Upload with ID {uploadId} not found"));

        // Act
        var result = await _controller.ResumeUpload(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ResumeUpload_WhenUploadIsNotIncomplete_ShouldReturnBadRequest()
    {
        // Arrange
        _uploadManagementServiceMock
            .Setup(x => x.ResumeUploadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ResumeUploadResponse>.Failure("Upload is not incomplete or cannot be resumed"));

        // Act
        var result = await _controller.ResumeUpload(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        value.Error.Should().Be("Upload is not incomplete or cannot be resumed");
    }

    [Fact]
    public async Task ResumeUpload_WhenStoragePathIsEmpty_ShouldReturnBadRequest()
    {
        // Arrange
        _uploadManagementServiceMock
            .Setup(x => x.ResumeUploadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ResumeUploadResponse>.Failure("Upload does not have a storage path and cannot be resumed"));

        // Act
        var result = await _controller.ResumeUpload(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        value.Error.Should().Be("Upload does not have a storage path and cannot be resumed");
    }

    [Fact]
    public async Task ResumeUpload_WhenSuccessful_ShouldReturnOkAndEnqueueUpload()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var resumeResponse = new ResumeUploadResponse
        {
            Message = "Upload re-enqueued for processing",
            UploadId = uploadId,
            WillResumeFromLine = 50,
            TotalLineCount = 100,
            ProcessedLineCount = 50
        };

        _uploadManagementServiceMock
            .Setup(x => x.ResumeUploadAsync(uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ResumeUploadResponse>.Success(resumeResponse));

        // Act
        var result = await _controller.ResumeUpload(uploadId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<ResumeUploadResponse>().Subject;

        value.Message.Should().Be("Upload re-enqueued for processing");
        value.UploadId.Should().Be(uploadId);
        value.WillResumeFromLine.Should().Be(50);
        value.TotalLineCount.Should().Be(100);
        value.ProcessedLineCount.Should().Be(50);

        _uploadManagementServiceMock.Verify(
            x => x.ResumeUploadAsync(uploadId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ResumeAllIncompleteUploads Tests

    [Fact]
    public async Task ResumeAllIncompleteUploads_WhenSuccessful_ShouldReturnOkWithResumedCount()
    {
        // Arrange
        var resumeResponse = new ResumeAllUploadsResponse
        {
            Message = "Resumed 2 incomplete upload(s)",
            ResumedCount = 2,
            ResumedUploads = new List<ResumedUploadInfo>
            {
                new ResumedUploadInfo
                {
                    UploadId = Guid.NewGuid(),
                    FileName = "file1.txt",
                    WillResumeFromLine = 50,
                    TotalLineCount = 100,
                    ProcessedLineCount = 50
                },
                new ResumedUploadInfo
                {
                    UploadId = Guid.NewGuid(),
                    FileName = "file2.txt",
                    WillResumeFromLine = 100,
                    TotalLineCount = 200,
                    ProcessedLineCount = 100
                }
            },
            Errors = null
        };

        _uploadManagementServiceMock
            .Setup(x => x.ResumeAllIncompleteUploadsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resumeResponse);

        // Act
        var result = await _controller.ResumeAllIncompleteUploads(30, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<ResumeAllUploadsResponse>().Subject;

        value.Message.Should().Be("Resumed 2 incomplete upload(s)");
        value.ResumedCount.Should().Be(2);
        value.ResumedUploads.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResumeAllIncompleteUploads_WhenUploadHasNoStoragePath_ShouldSkipAndAddError()
    {
        // Arrange
        var resumeResponse = new ResumeAllUploadsResponse
        {
            Message = "Resumed 1 incomplete upload(s)",
            ResumedCount = 1,
            ResumedUploads = new List<ResumedUploadInfo>
            {
                new ResumedUploadInfo
                {
                    UploadId = Guid.NewGuid(),
                    FileName = "file2.txt",
                    WillResumeFromLine = 100,
                    TotalLineCount = 200,
                    ProcessedLineCount = 100
                }
            },
            Errors = new List<string> { "Upload {uploadId} does not have a storage path" }
        };

        _uploadManagementServiceMock
            .Setup(x => x.ResumeAllIncompleteUploadsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resumeResponse);

        // Act
        var result = await _controller.ResumeAllIncompleteUploads(30, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<ResumeAllUploadsResponse>().Subject;

        value.ResumedCount.Should().Be(1); // Only upload2 was resumed
        value.Errors.Should().NotBeNull().And.HaveCount(1);
    }

    [Fact]
    public async Task ResumeAllIncompleteUploads_WhenExceptionOccurs_ShouldLogAndContinue()
    {
        // Arrange
        var resumeResponse = new ResumeAllUploadsResponse
        {
            Message = "Resumed 1 incomplete upload(s)",
            ResumedCount = 1,
            ResumedUploads = new List<ResumedUploadInfo>
            {
                new ResumedUploadInfo
                {
                    UploadId = Guid.NewGuid(),
                    FileName = "file2.txt",
                    WillResumeFromLine = 100,
                    TotalLineCount = 200,
                    ProcessedLineCount = 100
                }
            },
            Errors = new List<string> { "Error resuming upload {uploadId}: Queue error" }
        };

        _uploadManagementServiceMock
            .Setup(x => x.ResumeAllIncompleteUploadsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resumeResponse);

        // Act
        var result = await _controller.ResumeAllIncompleteUploads(30, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<ResumeAllUploadsResponse>().Subject;

        value.ResumedCount.Should().Be(1); // Only upload2 succeeded
        value.Errors.Should().NotBeNull().And.HaveCount(1);
    }

    #endregion

    #region GetTransactionsGroupedByStore Tests

    [Fact]
    public async Task GetTransactionsGroupedByStore_WhenSuccessful_ShouldReturnOkWithGroupedTransactions()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var groupedTransactions = new List<StoreGroupedTransactions>
        {
            new StoreGroupedTransactions
            {
                StoreName = "BAR DO JOÃO",
                StoreOwner = "096.206.760-17",
                Transactions = new List<Transaction>
                {
                    CreateTransaction("3", 142.00m, "096.206.760-17")
                },
                Balance = -102.00m
            }
        };

        var pagedResponse = new PagedResponse<StoreGroupedTransactions>
        {
            Items = groupedTransactions,
            TotalCount = 1,
            Page = 1,
            PageSize = 50,
            TotalPages = 1
        };

        _facadeServiceMock
            .Setup(x => x.GetTransactionsGroupedByStoreAsync(uploadId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResponse<StoreGroupedTransactions>>.Success(pagedResponse));

        // Act
        var result = await _controller.GetTransactionsGroupedByStore(uploadId, 1, 50, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value.Should().BeOfType<PagedResponse<StoreGroupedTransactions>>().Subject;
        value.Items.Should().BeEquivalentTo(groupedTransactions);
        value.TotalCount.Should().Be(1);
        value.Page.Should().Be(1);
        value.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task GetTransactionsGroupedByStore_WhenServiceFails_ShouldReturnBadRequest()
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        _facadeServiceMock
            .Setup(x => x.GetTransactionsGroupedByStoreAsync(uploadId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResponse<StoreGroupedTransactions>>.Failure("Error getting transactions"));

        // Act
        var result = await _controller.GetTransactionsGroupedByStore(uploadId, 1, 50, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("Error getting transactions");
    }

    [Fact]
    public async Task GetTransactionsGroupedByStore_WhenNoTransactionsFound_ShouldReturnNotFound()
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        var emptyPagedResponse = new PagedResponse<StoreGroupedTransactions>
        {
            Items = new List<StoreGroupedTransactions>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50,
            TotalPages = 0
        };

        _facadeServiceMock
            .Setup(x => x.GetTransactionsGroupedByStoreAsync(uploadId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResponse<StoreGroupedTransactions>>.Success(emptyPagedResponse));

        // Act
        var result = await _controller.GetTransactionsGroupedByStore(uploadId, 1, 50, CancellationToken.None);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var value = notFoundResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("No transactions found for this upload");
    }

    [Fact]
    public async Task GetTransactionsGroupedByStore_WhenDataIsNull_ShouldReturnNotFound()
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        _facadeServiceMock
            .Setup(x => x.GetTransactionsGroupedByStoreAsync(uploadId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResponse<StoreGroupedTransactions>>.Success(null!));

        // Act
        var result = await _controller.GetTransactionsGroupedByStore(uploadId, 1, 50, CancellationToken.None);

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var value = notFoundResult.Value;
        var errorProperty = value!.GetType().GetProperty("error");
        errorProperty!.GetValue(value).Should().Be("No transactions found for this upload");
    }

    #endregion

    #region Helper Methods

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

    private static FileUpload CreateFileUpload(
        Guid id,
        string fileName,
        FileUploadStatus status,
        int totalLineCount,
        int processedLineCount,
        int failedLineCount,
        int skippedLineCount)
    {
        return new FileUpload
        {
            Id = id,
            FileName = fileName,
            Status = status,
            TotalLineCount = totalLineCount,
            ProcessedLineCount = processedLineCount,
            FailedLineCount = failedLineCount,
            SkippedLineCount = skippedLineCount,
            LastCheckpointLine = processedLineCount,
            LastCheckpointAt = DateTime.UtcNow,
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-10),
            UploadedAt = DateTime.UtcNow.AddMinutes(-15),
            FileSize = 1024,
            FileHash = "test-hash",
            StoragePath = "path/to/file.txt"
        };
    }

    private void SetupControllerHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW";
        httpContext.Request.Body = new MemoryStream();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #endregion
}
