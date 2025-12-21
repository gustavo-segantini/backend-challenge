using CnabApi.Utilities;
using FluentAssertions;

namespace CnabApi.Tests.Utilities;

public class CorrelationIdHelperTests
{
    [Fact]
    public void GetOrCreateCorrelationId_WhenCalled_ReturnsValidGuid()
    {
        // Act
        var correlationId = CorrelationIdHelper.GetOrCreateCorrelationId();

        // Assert
        correlationId.Should().NotBeNullOrEmpty();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateCorrelationId_WhenCalledTwice_ReturnsSameValue()
    {
        // Act
        var firstCall = CorrelationIdHelper.GetOrCreateCorrelationId();
        var secondCall = CorrelationIdHelper.GetOrCreateCorrelationId();

        // Assert
        firstCall.Should().Be(secondCall);
    }

    [Fact]
    public void SetCorrelationId_WithValidGuid_StoresTheValue()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();

        // Act
        CorrelationIdHelper.SetCorrelationId(testId);
        var retrieved = CorrelationIdHelper.GetCorrelationId();

        // Assert
        retrieved.Should().Be(testId);
    }

    [Fact]
    public void GetCorrelationId_AfterSetting_ReturnsSetValue()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        CorrelationIdHelper.SetCorrelationId(testId);

        // Act
        var result = CorrelationIdHelper.GetCorrelationId();

        // Assert
        result.Should().Be(testId);
    }

    [Fact]
    public void GetCorrelationId_WithoutSetting_ReturnsNull()
    {
        // Note: This test assumes CorrelationIdHelper uses AsyncLocal or similar
        // which is isolated per test context. Behavior may vary based on implementation.
        
        // Act & Assert
        var result = CorrelationIdHelper.GetCorrelationId();
        // If not set, GetCorrelationId should return null
        result.Should().BeNull();
    }

    [Fact]
    public void CorrelationIdHeaderName_HasExpectedValue()
    {
        // Assert
        CorrelationIdHelper.CorrelationIdHeaderName.Should().Be("X-Correlation-ID");
    }

    [Fact]
    public void SetCorrelationId_WithCustomValue_UpdatesStoredValue()
    {
        // Arrange
        var firstId = Guid.NewGuid().ToString();
        var secondId = Guid.NewGuid().ToString();

        // Act
        CorrelationIdHelper.SetCorrelationId(firstId);
        CorrelationIdHelper.SetCorrelationId(secondId);
        var result = CorrelationIdHelper.GetCorrelationId();

        // Assert
        result.Should().Be(secondId);
        result.Should().NotBe(firstId);
    }

    [Fact]
    public void GetOrCreateCorrelationId_WithDifferentIds_EachReturnsDifferentGuid()
    {
        // Get first ID
        var firstId = CorrelationIdHelper.GetOrCreateCorrelationId();

        // Set a different ID manually
        var customId = Guid.NewGuid().ToString();
        CorrelationIdHelper.SetCorrelationId(customId);

        // Act
        var retrievedId = CorrelationIdHelper.GetCorrelationId();

        // Assert
        retrievedId.Should().Be(customId);
        retrievedId.Should().NotBe(firstId);
    }
}
