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

    [Theory]
    [InlineData("test.txt", "Valid CNAB content here")]
    [InlineData("TEST.TXT", "Valid content")] // Uppercase extension
    [InlineData("file.Txt", "Mixed case extension")]
    public async Task ReadCnabFileAsync_WithValidTxtFile_ShouldReturnSuccess(string fileName, string content)
    {
        // Arrange
        var file = CreateMockFormFile(fileName, content);

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(content);
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
        result.ErrorMessage.Should().Contain("provided");
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
        result.ErrorMessage.Should().Contain("provided");
    }

    [Theory]
    [InlineData("test.pdf", ".txt")]
    [InlineData("malicious.exe", null)]
    [InlineData("noextension", null)]
    [InlineData("test.doc", null)]
    [InlineData("test.xlsx", null)]
    [InlineData("test.csv", null)]
    [InlineData("test.json", null)]
    [InlineData("test.xml", null)]
    public async Task ReadCnabFileAsync_WithInvalidOrDisallowedExtensions_ShouldReturnFailure(string fileName, string? expectedErrorSubstring)
    {
        // Arrange
        var file = CreateMockFormFile(fileName, "Some content");

        // Act
        var result = await _fileService.ReadCnabFileAsync(file);

        // Assert
        result.IsSuccess.Should().BeFalse();
        if (expectedErrorSubstring != null)
        {
            result.ErrorMessage.Should().Contain(expectedErrorSubstring);
        }
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
        result.ErrorMessage.Should().Contain("empty");
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
        result.ErrorMessage.Should().Contain("exceeds the maximum");
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
