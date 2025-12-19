using CnabApi.Common;
using CnabApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for the FileService.
/// Tests file validation and reading functionality.
/// </summary>
public class FileServiceTests
{
    private readonly FileService _fileService;

    public FileServiceTests()
    {
        _fileService = new FileService();
    }

    #region ReadCnabFileAsync - Success Cases

    [Fact]
    public async Task ReadCnabFileAsync_WithValidTxtFile_ShouldReturnSuccess()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "Valid CNAB content here");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("Valid CNAB content here");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithUppercaseExtension_ShouldReturnSuccess()
    {
        // Arrange - FileService uses ToLowerInvariant(), so .TXT becomes .txt
        var file = CreateMockFormFile("TEST.TXT", "Valid content");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithMultiLineContent_ShouldPreserveContent()
    {
        // Arrange
        var content = "Line 1\nLine 2\nLine 3";
        var file = CreateMockFormFile("test.txt", content);

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Contain("Line 1");
        result.Data.Should().Contain("Line 2");
        result.Data.Should().Contain("Line 3");
    }

    #endregion

    #region ReadCnabFileAsync - Failure Cases

    [Fact]
    public async Task ReadCnabFileAsync_WithNullFile_ShouldReturnFailure()
    {
        // Arrange
        IFormFile? file = null;

        // Act
        var result = await _fileService.ReadCnabFileAsync(file!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("fornecido");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithEmptyFile_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("empty.txt", "");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("fornecido");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithInvalidExtension_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "Some content");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain(".txt");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithExeFile_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("malicious.exe", "Some content");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithNoExtension_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("noextension", "Some content");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(".doc")]
    [InlineData(".xlsx")]
    [InlineData(".csv")]
    [InlineData(".json")]
    [InlineData(".xml")]
    public async Task ReadCnabFileAsync_WithDisallowedExtensions_ShouldReturnFailure(string extension)
    {
        // Arrange
        var file = CreateMockFormFile($"test{extension}", "Some content");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithWhitespaceOnlyContent_ShouldReturnFailure()
    {
        // Arrange
        var file = CreateMockFormFile("test.txt", "   \n   \n   ");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("vazio");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithFileTooLarge_ShouldReturnFailure()
    {
        // Arrange - Create a file larger than 1MB (MaxFileSizeBytes)
        var largeContent = new string('A', 1024 * 1024 + 1); // 1MB + 1 byte
        var file = CreateMockFormFile("large.txt", largeContent);

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("excede o tamanho máximo");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WhenStreamThrowsException_ShouldReturnFailure()
    {
        // Arrange - Create a mock file that throws exception when reading
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("test.txt");
        mockFile.Setup(f => f.Length).Returns(100);
        mockFile.Setup(f => f.OpenReadStream()).Throws(new IOException("Simulated read error"));

        // Act
        var result = await _fileService.ReadCnabFileAsync(mockFile.Object);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Error reading file");
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

    #endregion
}
