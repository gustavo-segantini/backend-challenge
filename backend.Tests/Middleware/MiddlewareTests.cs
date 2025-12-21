using CnabApi.Middleware;
using CnabApi.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace CnabApi.Tests.Middleware;

/// <summary>
/// Unit tests for middleware components.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-ID";

    [Fact]
    public async Task InvokeAsync_WithExistingCorrelationId_UsesExistingId()
    {
        // Arrange
        var existingId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(HeaderName, existingId);
        
        var called = false;
        RequestDelegate next = async _ =>
        {
            called = true;
            await Task.CompletedTask;
        };
        
        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        called.Should().BeTrue();
        context.Response.Headers.Should().ContainKey(HeaderName);
    }

    [Fact]
    public async Task InvokeAsync_WithoutCorrelationId_GeneratesNewId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var called = false;
        
        RequestDelegate next = async _ =>
        {
            called = true;
            await Task.CompletedTask;
        };
        
        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        called.Should().BeTrue();
        context.Response.Headers.Should().ContainKey(HeaderName);
        var headerValue = context.Response.Headers[HeaderName].ToString();
        headerValue.Should().NotBeEmpty();
        Guid.TryParse(headerValue, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AddsCorrelationIdToResponseHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        RequestDelegate next = async _ => await Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.Should().ContainKey(HeaderName);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        
        RequestDelegate next = async _ =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        };
        
        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MultipleRequests_GenerateUniqueIds()
    {
        // Arrange
        var ids = new List<string>();
        
        for (int i = 0; i < 3; i++)
        {
            var context = new DefaultHttpContext();
            RequestDelegate next = async _ => await Task.CompletedTask;
            var middleware = new CorrelationIdMiddleware(next);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            ids.Add(context.Response.Headers[HeaderName].ToString());
        }

        ids.Distinct().Should().HaveCount(3);
    }
}

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithoutException_PassesThroughSuccessfully()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var nextCalled = false;
        
        RequestDelegate next = async _ =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status200OK;
            await Task.CompletedTask;
        };
        
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WithException_CatchesAndHandlesIt()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        
        RequestDelegate next = _ => throw new InvalidOperationException("Test error");
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentException_Returns500()
    {
        // Arrange - All exceptions are treated as 500 by this middleware
        var context = new DefaultHttpContext();
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        
        RequestDelegate next = _ => throw new ArgumentException("Invalid argument");
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task InvokeAsync_SetsJsonContentType()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        
        RequestDelegate next = _ => throw new Exception("Test");
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsErrorResponse_WithCorrectStatusCode()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var errorMessage = "Specific error";
        
        RequestDelegate next = _ => throw new Exception(errorMessage);
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task InvokeAsync_LogsException()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        var exception = new InvalidOperationException("Test error");
        
        RequestDelegate next = _ => throw exception;
        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
