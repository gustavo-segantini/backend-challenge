using CnabApi.Utilities;

namespace CnabApi.Tests.Utilities;

public class CursorPaginationHelperTests
{
    #region EncodeCursor Tests

    [Fact]
    public void EncodeCursor_WithValidString_ReturnsBase64EncodedString()
    {
        // Arrange
        var value = "test-cursor-123";

        // Act
        var result = CursorPaginationHelper.EncodeCursor(value);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // Verify it's valid base64 by decoding it back
        var decoded = Convert.FromBase64String(result);
        var decodedString = System.Text.Encoding.UTF8.GetString(decoded);
        Assert.Equal(value, decodedString);
    }

    [Fact]
    public void EncodeCursor_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = CursorPaginationHelper.EncodeCursor(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void EncodeCursor_WithNull_ReturnsNull()
    {
        // Act
        var result = CursorPaginationHelper.EncodeCursor(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void EncodeCursor_WithSpecialCharacters_EncodesCorrectly()
    {
        // Arrange
        var value = "cursor-with-special-chars-@#$%^&*()";

        // Act
        var result = CursorPaginationHelper.EncodeCursor(value);

        // Assert
        Assert.NotNull(result);
        var decoded = Convert.FromBase64String(result);
        var decodedString = System.Text.Encoding.UTF8.GetString(decoded);
        Assert.Equal(value, decodedString);
    }

    [Fact]
    public void EncodeCursor_WithUnicodeCharacters_EncodesCorrectly()
    {
        // Arrange
        var value = "cursor-with-unicode-😀-中文";

        // Act
        var result = CursorPaginationHelper.EncodeCursor(value);

        // Assert
        Assert.NotNull(result);
        var decoded = Convert.FromBase64String(result);
        var decodedString = System.Text.Encoding.UTF8.GetString(decoded);
        Assert.Equal(value, decodedString);
    }

    #endregion

    #region DecodeCursor Tests

    [Fact]
    public void DecodeCursor_WithValidBase64_ReturnsDecodedString()
    {
        // Arrange
        var original = "test-cursor-value";
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(original));

        // Act
        var result = CursorPaginationHelper.DecodeCursor(encoded);

        // Assert
        Assert.Equal(original, result);
    }

    [Fact]
    public void DecodeCursor_WithNull_ReturnsNull()
    {
        // Act
        var result = CursorPaginationHelper.DecodeCursor(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DecodeCursor_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = CursorPaginationHelper.DecodeCursor(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DecodeCursor_WithInvalidBase64_ReturnsNull()
    {
        // Arrange
        var invalidBase64 = "not-valid-base64-!!!";

        // Act
        var result = CursorPaginationHelper.DecodeCursor(invalidBase64);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DecodeCursor_RoundTrip_EncodeAndDecode_ReturnsSameValue()
    {
        // Arrange
        var original = "round-trip-test-value";

        // Act
        var encoded = CursorPaginationHelper.EncodeCursor(original);
        var decoded = CursorPaginationHelper.DecodeCursor(encoded);

        // Assert
        Assert.Equal(original, decoded);
    }

    #endregion

    #region CreatePaginatedResponse Tests

    [Fact]
    public void CreatePaginatedResponse_WithEmptyList_ReturnsEmptyResponse()
    {
        // Arrange
        var items = new List<string>();
        var pageSize = 10;

        // Act
        var result = CursorPaginationHelper.CreatePaginatedResponse(
            items,
            pageSize,
            null,
            item => item);

        // Assert
        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
        Assert.Null(result.PreviousCursor);
        Assert.False(result.HasMore);
    }

    [Fact]
    public void CreatePaginatedResponse_WithItemsLessThanPageSize_ReturnsAllItems()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3" };
        var pageSize = 10;

        // Act
        var result = CursorPaginationHelper.CreatePaginatedResponse(
            items,
            pageSize,
            null,
            item => item);

        // Assert
        Assert.Equal(3, result.Items.Count());
        Assert.Null(result.NextCursor);
        Assert.Null(result.PreviousCursor);
        Assert.False(result.HasMore);
    }

    [Fact]
    public void CreatePaginatedResponse_WithMoreItemsThanPageSize_ReturnsPageAndNextCursor()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3", "item4", "item5" };
        var pageSize = 3;

        // Act
        var result = CursorPaginationHelper.CreatePaginatedResponse(
            items,
            pageSize,
            null,
            item => item);

        // Assert
        Assert.Equal(3, result.Items.Count());
        Assert.NotNull(result.NextCursor);
        Assert.Null(result.PreviousCursor);
        Assert.True(result.HasMore);
        Assert.Equal("item3", result.Items.Last());
    }

    [Fact]
    public void CreatePaginatedResponse_WithCurrentCursor_ReturnsPreviousCursor()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3" };
        var pageSize = 10;
        var currentCursor = "previous-cursor-value";

        // Act
        var result = CursorPaginationHelper.CreatePaginatedResponse(
            items,
            pageSize,
            currentCursor,
            item => item);

        // Assert
        Assert.Equal(currentCursor, result.PreviousCursor);
    }

    [Fact]
    public void CreatePaginatedResponse_NextCursorIsEncodedCorrectly()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3", "item4" };
        var pageSize = 2;

        // Act
        var result = CursorPaginationHelper.CreatePaginatedResponse(
            items,
            pageSize,
            null,
            item => item);

        // Assert
        Assert.NotNull(result.NextCursor);
        var decoded = CursorPaginationHelper.DecodeCursor(result.NextCursor);
        Assert.Equal("item2", decoded);
    }

    [Fact]
    public void CreatePaginatedResponse_WithCustomCursorSelector_UsesSelectionForNextCursor()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new TestItem { Id = 1, Name = "first" },
            new TestItem { Id = 2, Name = "second" },
            new TestItem { Id = 3, Name = "third" }
        };
        var pageSize = 2;

        // Act
        var result = CursorPaginationHelper.CreatePaginatedResponse(
            items,
            pageSize,
            null,
            item => item.Id.ToString());

        // Assert
        Assert.NotNull(result.NextCursor);
        var decoded = CursorPaginationHelper.DecodeCursor(result.NextCursor);
        Assert.Equal("2", decoded);
    }

    [Fact]
    public void CreatePaginatedResponse_ExactlyPageSizeItems_HasMoreIsFalse()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3" };
        var pageSize = 3;

        // Act
        var result = CursorPaginationHelper.CreatePaginatedResponse(
            items,
            pageSize,
            null,
            item => item);

        // Assert
        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public void CreatePaginatedResponse_PageSizePlusOneItems_HasMoreIsTrue()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3", "item4" };
        var pageSize = 3;

        // Act
        var result = CursorPaginationHelper.CreatePaginatedResponse(
            items,
            pageSize,
            null,
            item => item);

        // Assert
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    #endregion

    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
