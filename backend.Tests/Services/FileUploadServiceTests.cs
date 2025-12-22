using CnabApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for FileUploadService
/// </summary>
public class FileUploadServiceTests
{
    private readonly Mock<ILogger<FileUploadService>> _loggerMock;
    private readonly FileUploadService _service;

    public FileUploadServiceTests()
    {
        _loggerMock = new Mock<ILogger<FileUploadService>>();
        _service = new FileUploadService(_loggerMock.Object);
    }

    #region ReadCnabFileFromMultipartAsync Tests

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithValidTextFile_ShouldReturnSuccess()
    {
        // Arrange
        var fileContent = "Test CNAB content\nLine 2\nLine 3";
        var (stream, reader) = CreateMultipartReader(fileContent, "test.txt");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(fileContent);
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithNoSection_ShouldReturnFailure()
    {
        // Arrange
        var emptyStream = new MemoryStream();
        var reader = new MultipartReader("boundary", emptyStream);

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("An unexpected error occurred while reading the file");
        
        emptyStream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithEmptyFile_ShouldReturnFailure()
    {
        // Arrange
        var (stream, reader) = CreateMultipartReader("", "empty.txt");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File was not provided or is empty");
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithInvalidExtension_ShouldReturnFailure()
    {
        // Arrange
        var (stream, reader) = CreateMultipartReader("Some content", "document.pdf");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only .txt files are allowed");
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithXmlExtension_ShouldReturnFailure()
    {
        // Arrange
        var (stream, reader) = CreateMultipartReader("Some content", "data.xml");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only .txt files are allowed");
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithNoFilename_ShouldReturnFailure()
    {
        // Arrange
        var boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";
        var content = $"------WebKitFormBoundary7MA4YWxkTrZu0gW\r\n" +
                     $"Content-Disposition: form-data; name=\"file\"\r\n" +
                     $"Content-Type: text/plain\r\n\r\n" +
                     $"Some content\r\n" +
                     $"------WebKitFormBoundary7MA4YWxkTrZu0gW--\r\n";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var reader = new MultipartReader(boundary, stream);

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File name is required");
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithLargeValidFile_ShouldReturnSuccess()
    {
        // Arrange - Create a large but valid file (under 1GB)
        var largeContent = new StringBuilder();
        for (int i = 0; i < 10000; i++)
        {
            largeContent.AppendLine($"Line {i}: Sample CNAB transaction data");
        }
        
        var (stream, reader) = CreateMultipartReader(largeContent.ToString(), "large.txt");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Contain("Line 0:");
        result.Data.Should().Contain("Line 9999:");
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithSpecialCharacters_ShouldReturnSuccess()
    {
        // Arrange
        var fileContent = "CNAB with special chars: çãõáéíóú ñ ü\n" +
                         "Symbols: @#$%¨&*()_+-={}[]|\\:;\"'<>,.?/\n" +
                         "Numbers: 0123456789";
        
        var (stream, reader) = CreateMultipartReader(fileContent, "special.txt");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Contain("çãõáéíóú");
        result.Data.Should().Contain("@#$%");
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithMultilineContent_ShouldPreserveLines()
    {
        // Arrange
        var fileContent = "Line 1\nLine 2\nLine 3\nLine 4\nLine 5";
        var (stream, reader) = CreateMultipartReader(fileContent, "multiline.txt");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(fileContent);
        result.Data!.Split('\n').Should().HaveCount(5);
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithCaseSensitiveExtension_ShouldAcceptTXT()
    {
        // Arrange
        var (stream, reader) = CreateMultipartReader("Content", "file.TXT");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("Content");
        
        stream.Dispose();
    }

    [Fact]
    public async Task ReadCnabFileFromMultipartAsync_WithCaseSensitiveExtension_ShouldAcceptTxt()
    {
        // Arrange
        var (stream, reader) = CreateMultipartReader("Content", "file.Txt");

        // Act
        var result = await _service.ReadCnabFileFromMultipartAsync(reader);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("Content");
        
        stream.Dispose();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a MultipartReader with the specified content and filename
    /// </summary>
    private static (MemoryStream stream, MultipartReader reader) CreateMultipartReader(string fileContent, string filename)
    {
        var boundary = "----WebKitFormBoundary7MA4YWxkTrZu0gW";
        
        var multipartContent = $"------WebKitFormBoundary7MA4YWxkTrZu0gW\r\n" +
                              $"Content-Disposition: form-data; name=\"file\"; filename=\"{filename}\"\r\n" +
                              $"Content-Type: text/plain\r\n\r\n" +
                              $"{fileContent}\r\n" +
                              $"------WebKitFormBoundary7MA4YWxkTrZu0gW--\r\n";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(multipartContent));
        var reader = new MultipartReader(boundary, stream);

        return (stream, reader);
    }

    #endregion
}
