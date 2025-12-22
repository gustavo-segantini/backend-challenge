using FluentAssertions;
using Moq;
using CnabApi.Services;
using Microsoft.AspNetCore.Http;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for FileServiceExtensions validation methods
/// </summary>
public class FileServiceExtensionsTests
{
    #region ValidateFile Tests

    [Fact]
    public void ValidateFile_WithValidFile_ReturnsNull()
    {
        // Arrange
        var mockFile = CreateMockFile("test.txt", "some content");

        // Act
        var result = FileServiceExtensions.ValidateFile(mockFile);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateFile_WithNullFile_ReturnsFileNotProvidedError()
    {
        // Act
        var result = FileServiceExtensions.ValidateFile(null!);

        // Assert
        result.Should().NotBeNull();
        result!.Message.Should().Contain("File was not provided or is empty");
    }

    [Fact]
    public void ValidateFile_WithEmptyFile_ReturnsFileNotProvidedError()
    {
        // Arrange
        var mockFile = CreateMockFile("test.txt", "");

        // Act
        var result = FileServiceExtensions.ValidateFile(mockFile);

        // Assert
        result.Should().NotBeNull();
        result!.Message.Should().Contain("File was not provided or is empty");
    }

    [Fact]
    public void ValidateFile_WithFileTooLarge_ReturnsFileTooLargeError()
    {
        // Arrange
        var mockFile = CreateMockFile("large.txt", new string('a', 2000000)); // 2MB

        // Act
        var result = FileServiceExtensions.ValidateFile(mockFile);

        // Assert
        result.Should().NotBeNull();
        result!.Message.Should().Contain("exceeds the maximum allowed size");
    }

    [Fact]
    public void ValidateFile_WithInvalidExtension_ReturnsUnsupportedMediaTypeError()
    {
        // Arrange - test various invalid extensions
        var invalidFiles = new[] 
        { 
            CreateMockFile("file.pdf", "content"),
            CreateMockFile("file.docx", "content"),
            CreateMockFile("file.csv", "content"),
            CreateMockFile("file.json", "content")
        };

        foreach (var file in invalidFiles)
        {
            // Act
            var result = FileServiceExtensions.ValidateFile(file);

            // Assert
            result.Should().NotBeNull();
            result!.Message.Should().Contain("Only files with extension '.txt' are allowed");
        }
    }

    [Fact]
    public void ValidateFile_WithCaseMixedTxtExtension_AcceptsFile()
    {
        // Arrange
        var mockFile = CreateMockFile("test.TXT", "content");

        // Act
        var result = FileServiceExtensions.ValidateFile(mockFile);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateFile_WithMaximumFileSize_Passes()
    {
        // Arrange
        var content = new string('a', 1024 * 1024); // Exactly 1MB
        var mockFile = CreateMockFile("test.txt", content);

        // Act
        var result = FileServiceExtensions.ValidateFile(mockFile);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateFile_ErrorIncludesFileExtensionInMessage()
    {
        // Arrange
        var mockFile = CreateMockFile("test.pdf", "content");

        // Act
        var result = FileServiceExtensions.ValidateFile(mockFile);

        // Assert
        result!.Message.Should().Contain(".pdf");
    }

    #endregion

    #region ValidateContent Tests

    [Fact]
    public void ValidateContent_WithValidCnabContent_ReturnsNull()
    {
        // Arrange
        var validContent = "Header line\nTransaction line";

        // Act
        var result = FileServiceExtensions.ValidateContent(validContent);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateContent_WithEmptyContent_ReturnsFileNotProvidedError()
    {
        // Act
        var result = FileServiceExtensions.ValidateContent("");

        // Assert
        result.Should().NotBeNull();
        result!.Message.Should().Contain("empty or contains only whitespace");
    }

    [Fact]
    public void ValidateContent_WithWhitespaceOnly_ReturnsFileNotProvidedError()
    {
        // Act
        var result = FileServiceExtensions.ValidateContent("   \n  \n  ");

        // Assert
        result.Should().NotBeNull();
        result!.Message.Should().Contain("empty or contains only whitespace");
    }

    [Fact]
    public void ValidateContent_WithNullContent_ReturnsFileNotProvidedError()
    {
        // Act
        var result = FileServiceExtensions.ValidateContent(null!);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void ValidateContent_WithOnlyOneLine_ReturnsInvalidContentError()
    {
        // Arrange
        var singleLine = "Only one line";

        // Act
        var result = FileServiceExtensions.ValidateContent(singleLine);

        // Assert
        result.Should().NotBeNull();
        result!.Message.Should().Contain("must contain at least one header and one transaction record");
    }

    [Fact]
    public void ValidateContent_WithMultipleLines_ReturnsNull()
    {
        // Arrange
        var multiLineContent = "Line 1\nLine 2\nLine 3\nLine 4";

        // Act
        var result = FileServiceExtensions.ValidateContent(multiLineContent);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateContent_WithDifferentLineEndings_HandlesCorrectly()
    {
        // Test different line ending formats
        var contentVariations = new[]
        {
            "Line1\nLine2",      // Unix
            "Line1\r\nLine2",    // Windows
            "Line1\rLine2"       // Old Mac
        };

        foreach (var content in contentVariations)
        {
            // Act
            var result = FileServiceExtensions.ValidateContent(content);

            // Assert
            result.Should().BeNull();
        }
    }

    [Fact]
    public void ValidateContent_WithExactlyTwoLines_Passes()
    {
        // Arrange
        var exactContent = "Header\nTransaction";

        // Act
        var result = FileServiceExtensions.ValidateContent(exactContent);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region EnhancedFileService Tests

    [Fact]
    public async Task ReadCnabFileAsync_WithValidFile_ReturnsSuccess()
    {
        // Arrange
        var service = new EnhancedFileService();
        var mockFile = CreateMockFile("test.txt", "Header line\nTransaction line");

        // Act
        var result = await service.ReadCnabFileAsync(mockFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Contain("Header line");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithEmptyFile_ReturnsFailure()
    {
        // Arrange
        var service = new EnhancedFileService();
        var mockFile = CreateMockFile("empty.txt", "");

        // Act
        var result = await service.ReadCnabFileAsync(mockFile);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File was not provided or is empty");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithInvalidExtension_ReturnsFailure()
    {
        // Arrange
        var service = new EnhancedFileService();
        var mockFile = CreateMockFile("file.pdf", "content");

        // Act
        var result = await service.ReadCnabFileAsync(mockFile);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only files with extension '.txt'");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithTooLargeFile_ReturnsFailure()
    {
        // Arrange
        var service = new EnhancedFileService();
        var largeContent = new string('a', 2000000); // 2MB
        var mockFile = CreateMockFile("large.txt", largeContent);

        // Act
        var result = await service.ReadCnabFileAsync(mockFile);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds the maximum allowed size");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithInvalidCnabContent_ReturnsFailure()
    {
        // Arrange
        var service = new EnhancedFileService();
        var mockFile = CreateMockFile("single.txt", "Only one line");

        // Act
        var result = await service.ReadCnabFileAsync(mockFile);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("must contain at least one header and one transaction record");
    }

    [Fact]
    public async Task ReadCnabFileAsync_WithValidCnabContent_ReturnsFileContent()
    {
        // Arrange
        var service = new EnhancedFileService();
        var content = "Header line\nTransaction line\nAnother transaction";
        var mockFile = CreateMockFile("valid.txt", content);

        // Act
        var result = await service.ReadCnabFileAsync(mockFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(content);
    }

    [Fact]
    public async Task ReadCnabFileAsync_PropagatesFileReadErrors()
    {
        // Arrange
        var service = new EnhancedFileService();
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(100); // arquivo tem tamanho
        mockFile.Setup(f => f.FileName).Returns("test.txt"); // extensão válida
        mockFile.Setup(f => f.OpenReadStream()).Throws<IOException>();

        // Act
        var result = await service.ReadCnabFileAsync(mockFile.Object);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Error reading file");
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void ValidateFile_And_ValidateContent_WorkTogether()
    {
        // Arrange
        var mockFile = CreateMockFile("test.txt", "Header\nTransaction");
        var fileError = FileServiceExtensions.ValidateFile(mockFile);

        // Act - if file is valid, check content
        var contentError = fileError == null 
            ? FileServiceExtensions.ValidateContent("Header\nTransaction")
            : null;

        // Assert
        fileError.Should().BeNull();
        contentError.Should().BeNull();
    }

    [Fact]
    public void MultipleValidationFailures_FirstErrorReturned()
    {
        // Arrange - file with multiple issues (too large + wrong extension)
        // ValidateFile verifica tamanho ANTES de extensão
        var mockFile = CreateMockFile("test.pdf", new string('a', 2000000));

        // Act
        var result = FileServiceExtensions.ValidateFile(mockFile);

        // Assert - should return size error first (checked before extension)
        result.Should().NotBeNull();
        result!.Message.Should().Contain("File exceeds the maximum allowed size");
    }

    [Theory]
    [InlineData("valid.txt")]
    [InlineData("VALID.TXT")]
    [InlineData("Valid.Txt")]
    public void ValidateFile_CaseInsensitiveExtension(string filename)
    {
        // Act
        var result = FileServiceExtensions.ValidateFile(CreateMockFile(filename, "content"));

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock IFormFile for testing
    /// </summary>
    private static IFormFile CreateMockFile(string filename, string content)
    {
        var mockFile = new Mock<IFormFile>();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        mockFile.Setup(f => f.FileName).Returns(filename);
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        return mockFile.Object;
    }

    #endregion
}
