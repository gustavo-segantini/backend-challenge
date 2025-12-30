using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Services.StatusCodes;
using FluentAssertions;

namespace CnabApi.Tests.Services.StatusCodes;

/// <summary>
/// Unit tests for UploadStatusCodeStrategyFactory and all status code strategies.
/// </summary>
public class UploadStatusCodeStrategyFactoryTests
{
    private readonly UploadStatusCodeStrategyFactory _factory;

    public UploadStatusCodeStrategyFactoryTests()
    {
        _factory = new UploadStatusCodeStrategyFactory();
    }

    #region UploadStatusCodeStrategyFactory Tests

    [Theory]
    [InlineData("File is empty", UploadStatusCode.BadRequest)]
    [InlineData("The file content is empty", UploadStatusCode.BadRequest)]
    [InlineData("EMPTY file provided", UploadStatusCode.BadRequest)]
    public void DetermineStatusCode_WithEmptyFileError_ShouldReturnBadRequest(string errorMessage, UploadStatusCode expected)
    {
        // Act
        var result = _factory.DetermineStatusCode(errorMessage);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("File extension not allowed", UploadStatusCode.UnsupportedMediaType)]
    [InlineData("Only .txt files are not allowed", UploadStatusCode.UnsupportedMediaType)]
    [InlineData("NOT ALLOWED extension", UploadStatusCode.UnsupportedMediaType)]
    public void DetermineStatusCode_WithUnsupportedMediaTypeError_ShouldReturnUnsupportedMediaType(string errorMessage, UploadStatusCode expected)
    {
        // Act
        var result = _factory.DetermineStatusCode(errorMessage);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("File exceeds maximum size", UploadStatusCode.PayloadTooLarge)]
    [InlineData("File size exceeds maximum size limit", UploadStatusCode.PayloadTooLarge)]
    [InlineData("EXCEEDS MAXIMUM SIZE", UploadStatusCode.PayloadTooLarge)]
    public void DetermineStatusCode_WithFileSizeError_ShouldReturnPayloadTooLarge(string errorMessage, UploadStatusCode expected)
    {
        // Act
        var result = _factory.DetermineStatusCode(errorMessage);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Invalid CNAB format", UploadStatusCode.UnprocessableEntity)]
    [InlineData("File format is invalid", UploadStatusCode.UnprocessableEntity)]
    [InlineData("INVALID line format", UploadStatusCode.UnprocessableEntity)]
    public void DetermineStatusCode_WithInvalidFormatError_ShouldReturnUnprocessableEntity(string errorMessage, UploadStatusCode expected)
    {
        // Act
        var result = _factory.DetermineStatusCode(errorMessage);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Some other error message")]
    [InlineData("Unknown error occurred")]
    public void DetermineStatusCode_WithUnknownError_ShouldReturnBadRequest(string? errorMessage)
    {
        // Act
        var result = _factory.DetermineStatusCode(errorMessage);

        // Assert
        result.Should().Be(UploadStatusCode.BadRequest);
    }

    [Fact]
    public void DetermineStatusCode_WithMultipleMatchingStrategies_ShouldUseFirstMatch()
    {
        // Arrange - Error that could match multiple strategies (empty + invalid)
        // EmptyFile strategy should match first
        const string errorMessage = "File is empty and invalid";

        // Act
        var result = _factory.DetermineStatusCode(errorMessage);

        // Assert
        result.Should().Be(UploadStatusCode.BadRequest); // EmptyFile strategy matches first
    }

    #endregion

    #region EmptyFileStatusCodeStrategy Tests

    [Theory]
    [InlineData("File is empty")]
    [InlineData("The file content is EMPTY")]
    [InlineData("empty file provided")]
    [InlineData("EMPTY")]
    public void EmptyFileStatusCodeStrategy_CanHandle_WithEmptyKeywords_ShouldReturnTrue(string errorMessage)
    {
        // Arrange
        var strategy = new EmptyFileStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("File has content")]
    [InlineData("Some other error")]
    public void EmptyFileStatusCodeStrategy_CanHandle_WithoutEmptyKeywords_ShouldReturnFalse(string? errorMessage)
    {
        // Arrange
        var strategy = new EmptyFileStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EmptyFileStatusCodeStrategy_GetStatusCode_ShouldReturnBadRequest()
    {
        // Arrange
        var strategy = new EmptyFileStatusCodeStrategy();

        // Act
        var result = strategy.GetStatusCode("File is empty");

        // Assert
        result.Should().Be(UploadStatusCode.BadRequest);
    }

    #endregion

    #region UnsupportedMediaTypeStatusCodeStrategy Tests

    [Theory]
    [InlineData("File extension not allowed")]
    [InlineData("Extension NOT ALLOWED")]
    [InlineData("not allowed extension")]
    public void UnsupportedMediaTypeStatusCodeStrategy_CanHandle_WithNotAllowedKeywords_ShouldReturnTrue(string errorMessage)
    {
        // Arrange
        var strategy = new UnsupportedMediaTypeStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("File extension is allowed")]
    [InlineData("Some other error")]
    public void UnsupportedMediaTypeStatusCodeStrategy_CanHandle_WithoutNotAllowedKeywords_ShouldReturnFalse(string? errorMessage)
    {
        // Arrange
        var strategy = new UnsupportedMediaTypeStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnsupportedMediaTypeStatusCodeStrategy_GetStatusCode_ShouldReturnUnsupportedMediaType()
    {
        // Arrange
        var strategy = new UnsupportedMediaTypeStatusCodeStrategy();

        // Act
        var result = strategy.GetStatusCode("File extension not allowed");

        // Assert
        result.Should().Be(UploadStatusCode.UnsupportedMediaType);
    }

    #endregion

    #region PayloadTooLargeStatusCodeStrategy Tests

    [Theory]
    [InlineData("File exceeds maximum size")]
    [InlineData("File size EXCEEDS MAXIMUM SIZE")]
    [InlineData("exceeds maximum size limit")]
    public void PayloadTooLargeStatusCodeStrategy_CanHandle_WithExceedsMaximumSizeKeywords_ShouldReturnTrue(string errorMessage)
    {
        // Arrange
        var strategy = new PayloadTooLargeStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("File size is within limits")]
    [InlineData("Some other error")]
    public void PayloadTooLargeStatusCodeStrategy_CanHandle_WithoutExceedsMaximumSizeKeywords_ShouldReturnFalse(string? errorMessage)
    {
        // Arrange
        var strategy = new PayloadTooLargeStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void PayloadTooLargeStatusCodeStrategy_GetStatusCode_ShouldReturnPayloadTooLarge()
    {
        // Arrange
        var strategy = new PayloadTooLargeStatusCodeStrategy();

        // Act
        var result = strategy.GetStatusCode("File exceeds maximum size");

        // Assert
        result.Should().Be(UploadStatusCode.PayloadTooLarge);
    }

    #endregion

    #region InvalidFormatStatusCodeStrategy Tests

    [Theory]
    [InlineData("Invalid CNAB format")]
    [InlineData("File format is INVALID")]
    [InlineData("invalid line")]
    public void InvalidFormatStatusCodeStrategy_CanHandle_WithInvalidKeywords_ShouldReturnTrue(string errorMessage)
    {
        // Arrange
        var strategy = new InvalidFormatStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("File format is valid")]
    [InlineData("Some other error")]
    public void InvalidFormatStatusCodeStrategy_CanHandle_WithoutInvalidKeywords_ShouldReturnFalse(string? errorMessage)
    {
        // Arrange
        var strategy = new InvalidFormatStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void InvalidFormatStatusCodeStrategy_GetStatusCode_ShouldReturnUnprocessableEntity()
    {
        // Arrange
        var strategy = new InvalidFormatStatusCodeStrategy();

        // Act
        var result = strategy.GetStatusCode("Invalid CNAB format");

        // Assert
        result.Should().Be(UploadStatusCode.UnprocessableEntity);
    }

    #endregion

    #region DefaultStatusCodeStrategy Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Any error message")]
    [InlineData("Unknown error")]
    public void DefaultStatusCodeStrategy_CanHandle_WithAnyMessage_ShouldReturnTrue(string? errorMessage)
    {
        // Arrange
        var strategy = new DefaultStatusCodeStrategy();

        // Act
        var result = strategy.CanHandle(errorMessage);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void DefaultStatusCodeStrategy_GetStatusCode_ShouldReturnBadRequest()
    {
        // Arrange
        var strategy = new DefaultStatusCodeStrategy();

        // Act
        var result = strategy.GetStatusCode("Any error");

        // Assert
        result.Should().Be(UploadStatusCode.BadRequest);
    }

    #endregion
}

