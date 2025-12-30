using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using CnabApi.Extensions;

namespace CnabApi.Tests.Extensions;

/// <summary>
/// Unit tests for ControllerBaseExtensions granular HTTP status code methods
/// </summary>
public class ControllerBaseExtensionsTests
{
    private class MockController : ControllerBase
    {
    }

    private readonly MockController _controller = new();

    #region FileTooLarge Tests

    [Fact]
    public void FileTooLarge_WithDefaultMessage_Returns413WithDefaultMessage()
    {
        // Act
        var result = _controller.FileTooLarge();

        // Assert
        result.StatusCode.Should().Be(413);
        var value = result.Value!;
        var errorProperty = value.GetType().GetProperty("error");
        errorProperty.Should().NotBeNull();
        var errorValue = errorProperty!.GetValue(value) as string;
        errorValue.Should().Contain("File size exceeds maximum allowed");
    }

    [Fact]
    public void FileTooLarge_WithCustomMessage_Returns413WithCustomMessage()
    {
        // Arrange
        const string customMessage = "File is too large for processing";

        // Act
        var result = _controller.FileTooLarge(customMessage);

        // Assert
        result.StatusCode.Should().Be(413);
        var value = result.Value!;
        var errorProperty = value.GetType().GetProperty("error");
        errorProperty.Should().NotBeNull();
        var errorValue = errorProperty!.GetValue(value) as string;
        errorValue.Should().Be(customMessage);
    }

    [Fact]
    public void FileTooLarge_ReturnsObjectResult()
    {
        // Act
        var result = _controller.FileTooLarge("test message");

        // Assert
        result.Should().BeOfType<ObjectResult>();
    }

    #endregion

    #region UnsupportedMediaType Tests

    [Fact]
    public void UnsupportedMediaType_WithDefaultMessage_Returns415WithDefaultMessage()
    {
        // Act
        var result = _controller.UnsupportedMediaType();

        // Assert
        result.StatusCode.Should().Be(415);
        var value = result.Value!;
        var errorProperty = value.GetType().GetProperty("error");
        errorProperty.Should().NotBeNull();
        var errorValue = errorProperty!.GetValue(value) as string;
        errorValue.Should().Contain("Please upload a .txt file");
    }

    [Fact]
    public void UnsupportedMediaType_WithCustomMessage_Returns415WithCustomMessage()
    {
        // Arrange
        const string customMessage = "Only CNAB .txt files are accepted";

        // Act
        var result = _controller.UnsupportedMediaType(customMessage);

        // Assert
        result.StatusCode.Should().Be(415);
        var value = result.Value!;
        var errorProperty = value.GetType().GetProperty("error");
        errorProperty.Should().NotBeNull();
        var errorValue = errorProperty!.GetValue(value) as string;
        errorValue.Should().Be(customMessage);
    }

    [Fact]
    public void UnsupportedMediaType_ReturnsObjectResult()
    {
        // Act
        var result = _controller.UnsupportedMediaType("message");

        // Assert
        result.Should().BeOfType<ObjectResult>();
    }

    #endregion

    #region UnprocessableEntity Tests

    [Fact]
    public void UnprocessableEntity_WithOnlyMessage_Returns422WithMessage()
    {
        // Arrange
        const string message = "Invalid CNAB format";

        // Act
        var result = ControllerBaseExtensions.UnprocessableEntity(_controller, message);

        // Assert
        result.StatusCode.Should().Be(422);
        var value = result.Value!;
        var errorProperty = value.GetType().GetProperty("error");
        errorProperty.Should().NotBeNull();
        var errorValue = errorProperty!.GetValue(value) as string;
        errorValue.Should().Be(message);
    }

    [Fact]
    public void UnprocessableEntity_WithMessageAndDetails_Returns422WithBoth()
    {
        // Arrange
        const string message = "Invalid CNAB format";
        var details = new { lineNumber = 5, reason = "Invalid field length" };

        // Act
        var result = ControllerBaseExtensions.UnprocessableEntity(_controller, message, details);

        // Assert
        result.StatusCode.Should().Be(422);
        var value = result.Value!;
        var errorProperty = value.GetType().GetProperty("error");
        errorProperty.Should().NotBeNull();
        var errorValue = errorProperty!.GetValue(value) as string;
        errorValue.Should().Be(message);
        var detailsProperty = value.GetType().GetProperty("details");
        detailsProperty.Should().NotBeNull();
    }

    [Fact]
    public void UnprocessableEntity_WithoutDetails_DoesNotIncludeDetailsProperty()
    {
        // Act
        var result = ControllerBaseExtensions.UnprocessableEntity(_controller, "message only");

        // Assert
        result.StatusCode.Should().Be(422);
        var value = result.Value!;
        var valueType = value.GetType();
        var detailsProperty = valueType.GetProperty("details");
        detailsProperty.Should().BeNull("details property should not exist when not provided");
    }

    #endregion

    #region InternalServerError Tests

    [Fact]
    public void InternalServerError_WithDefaultMessage_Returns500WithDefaultMessage()
    {
        // Act
        var result = _controller.InternalServerError();

        // Assert
        result.StatusCode.Should().Be(500);
        var value = result.Value!;
        var errorProperty = value.GetType().GetProperty("error");
        errorProperty.Should().NotBeNull();
        var errorValue = errorProperty!.GetValue(value) as string;
        errorValue.Should().Contain("unexpected error");
    }

    [Fact]
    public void InternalServerError_WithCustomMessage_Returns500WithCustomMessage()
    {
        // Arrange
        const string customMessage = "Database connection failed";

        // Act
        var result = _controller.InternalServerError(customMessage);

        // Assert
        result.StatusCode.Should().Be(500);
        var value = result.Value!;
        var errorProperty = value.GetType().GetProperty("error");
        errorProperty.Should().NotBeNull();
        var errorValue = errorProperty!.GetValue(value) as string;
        errorValue.Should().Be(customMessage);
    }

    [Fact]
    public void InternalServerError_ReturnsObjectResult()
    {
        // Act
        var result = _controller.InternalServerError("error message");

        // Assert
        result.Should().BeOfType<ObjectResult>();
    }

    #endregion

    #region Problem Tests

    [Fact]
    public void Problem_WithAllParameters_ReturnsProblemDetailsWithStatus()
    {
        // Arrange
        const string title = "Bad Request";
        const string detail = "Invalid input data";
        const int status = 400;
        const string instance = "/api/v1/transactions/upload";

        // Act
        var result = ControllerBaseExtensions.Problem(_controller, title, detail, status, instance);

        // Assert
        result.StatusCode.Should().Be(status);
        var response = result.Value as ProblemDetails;
        response.Should().NotBeNull();
        response!.Title.Should().Be(title);
        response.Detail.Should().Be(detail);
        response.Status.Should().Be(status);
        response.Instance.Should().Be(instance);
    }

    [Fact]
    public void Problem_WithoutInstance_ReturnsProblemDetailsWithoutInstance()
    {
        // Arrange
        const string title = "Conflict";
        const string detail = "Duplicate entry";
        const int status = 409;

        // Act
        var result = ControllerBaseExtensions.Problem(_controller, title, detail, status);

        // Assert
        result.StatusCode.Should().Be(status);
        var response = result.Value as ProblemDetails;
        response!.Title.Should().Be(title);
        response.Instance.Should().BeNull();
    }

    [Fact]
    public void Problem_ReturnsProblemDetailsRFC7807Format()
    {
        // Act
        var result = ControllerBaseExtensions.Problem(_controller, "Not Found", "Resource does not exist", 404);

        // Assert
        var response = result.Value as ProblemDetails;
        response.Should().NotBeNull();
        response!.Title.Should().NotBeNullOrEmpty();
        response.Detail.Should().NotBeNullOrEmpty();
        response.Status.Should().Be(404);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(500)]
    [InlineData(503)]
    public void Problem_WithDifferentStatusCodes_ReturnsCorrectStatusCode(int statusCode)
    {
        // Act
        var result = _controller.Problem("Error", "Details", statusCode);

        // Assert
        result.StatusCode.Should().Be(statusCode);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void MultipleExtensions_AreAllAvailable()
    {
        // Verify all extension methods are callable
        var methods = typeof(ControllerBaseExtensions)
            .GetMethods()
            .Where(m => m.ReturnType == typeof(ObjectResult))
            .ToList();

        // Assert - should have at least 5 extension methods
        methods.Count.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void AllStatusCodeResponses_ReturnObjectResult()
    {
        // Act & Assert - Usando BeAssignableTo para aceitar subclasses de ObjectResult
        _controller.FileTooLarge().Should().BeAssignableTo<ObjectResult>();
        _controller.UnsupportedMediaType().Should().BeAssignableTo<ObjectResult>();
        _controller.UnprocessableEntity("msg").Should().BeAssignableTo<ObjectResult>();
        _controller.InternalServerError().Should().BeAssignableTo<ObjectResult>();
        _controller.Problem("Title", "Detail", 400).Should().BeAssignableTo<ObjectResult>();
    }

    [Theory]
    [InlineData(413)]
    [InlineData(415)]
    [InlineData(422)]
    [InlineData(500)]
    public void AllExtensions_CanBeChainedInErrorHandling(int statusCode)
    {
        // Arrange
        ObjectResult result = statusCode switch
        {
            413 => _controller.FileTooLarge("File error"),
            415 => _controller.UnsupportedMediaType("Type error"),
            422 => _controller.UnprocessableEntity("Entity error"),
            500 => _controller.InternalServerError("Server error"),
            _ => throw new ArgumentOutOfRangeException(nameof(statusCode))
        };

        // Assert
        result.StatusCode.Should().Be(statusCode);
    }

    #endregion
}
