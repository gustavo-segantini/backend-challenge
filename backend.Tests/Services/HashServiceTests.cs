using CnabApi.Services;
using FluentAssertions;

namespace CnabApi.Tests.Services;

/// <summary>
/// Unit tests for the HashService.
/// Tests hash computation functionality for files, lines, and streams.
/// </summary>
public class HashServiceTests
{
    private readonly HashService _hashService;

    public HashServiceTests()
    {
        _hashService = new HashService();
    }

    #region ComputeFileHash Tests

    [Fact]
    public void ComputeFileHash_WithValidContent_ShouldReturnBase64Hash()
    {
        // Arrange
        const string content = "test content";

        // Act
        var result = _hashService.ComputeFileHash(content);

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Base64 can contain padding with "=", so we just verify it's a valid Base64 string
        result.Should().MatchRegex("^[A-Za-z0-9+/]+={0,2}$"); // Valid Base64 pattern
    }

    [Fact]
    public void ComputeFileHash_WithSameContent_ShouldReturnSameHash()
    {
        // Arrange
        const string content = "identical content";

        // Act
        var hash1 = _hashService.ComputeFileHash(content);
        var hash2 = _hashService.ComputeFileHash(content);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeFileHash_WithDifferentContent_ShouldReturnDifferentHash()
    {
        // Arrange
        const string content1 = "content 1";
        const string content2 = "content 2";

        // Act
        var hash1 = _hashService.ComputeFileHash(content1);
        var hash2 = _hashService.ComputeFileHash(content2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ComputeFileHash_WithNullOrEmptyContent_ShouldThrowArgumentException(string? content)
    {
        // Act & Assert
        var act = () => _hashService.ComputeFileHash(content!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("content");
    }

    [Theory]
    [InlineData("Test with unicode: 测试 🚀 ñoño")] // Unicode content
    [InlineData("Regular content")]
    [InlineData("1234567890")]
    public void ComputeFileHash_WithVariousContent_ShouldReturnHash(string content)
    {
        // Arrange & Act
        var result = _hashService.ComputeFileHash(content);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex("^[A-Za-z0-9+/]+={0,2}$");
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(100000)]
    [InlineData(1000)]
    public void ComputeFileHash_WithLargeContent_ShouldReturnHash(int size)
    {
        // Arrange
        var largeContent = new string('A', size);

        // Act
        var result = _hashService.ComputeFileHash(largeContent);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ComputeLineHash Tests

    [Fact]
    public void ComputeLineHash_WithValidLine_ShouldReturnHexHash()
    {
        // Arrange
        const string line = "3201903010000014200096206760174753****3153153453JOÃO MACEDO   BAR DO JOÃO       ";

        // Act
        var result = _hashService.ComputeLineHash(line);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex("^[0-9a-f]{64}$"); // SHA256 hex is 64 characters
    }

    [Fact]
    public void ComputeLineHash_WithSameLine_ShouldReturnSameHash()
    {
        // Arrange
        const string line = "identical line";

        // Act
        var hash1 = _hashService.ComputeLineHash(line);
        var hash2 = _hashService.ComputeLineHash(line);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeLineHash_WithDifferentLines_ShouldReturnDifferentHash()
    {
        // Arrange
        const string line1 = "line 1";
        const string line2 = "line 2";

        // Act
        var hash1 = _hashService.ComputeLineHash(line1);
        var hash2 = _hashService.ComputeLineHash(line2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ComputeLineHash_WithNullOrEmptyLine_ShouldThrowArgumentException(string? line)
    {
        // Act & Assert
        var act = () => _hashService.ComputeLineHash(line!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("line");
    }

    [Fact]
    public void ComputeLineHash_ShouldReturnLowercaseHex()
    {
        // Arrange
        const string line = "test line";

        // Act
        var result = _hashService.ComputeLineHash(line);

        // Assert
        result.Should().MatchRegex("^[0-9a-f]+$"); // Only lowercase hex digits
        result.Should().NotMatchRegex("[A-F]"); // No uppercase
    }

    #endregion

    #region ComputeStreamHashAsync Tests

    [Theory]
    [InlineData("test stream content")]
    [InlineData("")]
    [InlineData("large content with many characters")]
    public async Task ComputeStreamHashAsync_WithVariousContent_ShouldReturnHexHash(string content)
    {
        // Arrange
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        // Act
        var result = await _hashService.ComputeStreamHashAsync(stream);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().MatchRegex("^[0-9a-f]{64}$"); // SHA256 hex is 64 characters
    }

    [Fact]
    public async Task ComputeStreamHashAsync_WithSameContent_ShouldReturnSameHash()
    {
        // Arrange
        var content = "identical stream content";
        var stream1 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var stream2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        // Act
        var hash1 = await _hashService.ComputeStreamHashAsync(stream1);
        var hash2 = await _hashService.ComputeStreamHashAsync(stream2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public async Task ComputeStreamHashAsync_ShouldResetStreamPosition()
    {
        // Arrange
        var content = "test content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var initialPosition = stream.Position;

        // Act
        await _hashService.ComputeStreamHashAsync(stream);

        // Assert
        stream.Position.Should().Be(initialPosition);
    }

    [Fact]
    public async Task ComputeStreamHashAsync_WithStreamAtEnd_ShouldResetToBeginning()
    {
        // Arrange
        var content = "test content";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        stream.Seek(0, SeekOrigin.End); // Move to end

        // Act
        var result = await _hashService.ComputeStreamHashAsync(stream);

        // Assert
        result.Should().NotBeNullOrEmpty();
        stream.Position.Should().Be(0); // Should be reset to beginning
    }

    [Fact]
    public async Task ComputeStreamHashAsync_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Arrange
        Stream? stream = null;

        // Act & Assert
        var act = async () => await _hashService.ComputeStreamHashAsync(stream!);
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("stream");
    }

    [Fact]
    public async Task ComputeStreamHashAsync_WithNonSeekableStream_ShouldStillComputeHash()
    {
        // Arrange
        var content = "test content";
        var baseStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var nonSeekableStream = new NonSeekableStreamWrapper(baseStream);

        // Act
        var result = await _hashService.ComputeStreamHashAsync(nonSeekableStream);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ComputeStreamHashAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var content = new string('A', 100000); // Large content
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _hashService.ComputeStreamHashAsync(stream, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }


    #endregion

    #region Helper Classes

    private class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _baseStream;

        public NonSeekableStreamWrapper(Stream baseStream)
        {
            _baseStream = baseStream;
        }

        public override bool CanSeek => false;
        public override bool CanRead => _baseStream.CanRead;
        public override bool CanWrite => false;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #endregion
}

