using CnabApi.Middleware;
using CnabApi.Utilities;
using FluentAssertions;
using Moq;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace CnabApi.Tests.Middleware;

public class CorrelationIdEnricherTests
{
    [Fact]
    public void Enrich_WithCorrelationIdSet_CallsCreatePropertyWithCorrectValues()
    {
        // Arrange
        var testCorrelationId = Guid.NewGuid().ToString();
        CorrelationIdHelper.SetCorrelationId(testCorrelationId);

        var enricher = new CorrelationIdEnricher();
        var logEvent = new LogEvent(
            DateTime.Now,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("Test message"),
            Enumerable.Empty<LogEventProperty>());

        var propertyFactoryMock = new Mock<ILogEventPropertyFactory>();
        propertyFactoryMock
            .Setup(x => x.CreateProperty(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(new LogEventProperty("CorrelationId", new ScalarValue(testCorrelationId)));

        // Act
        enricher.Enrich(logEvent, propertyFactoryMock.Object);

        // Assert
        propertyFactoryMock.Verify(
            x => x.CreateProperty("CorrelationId", testCorrelationId),
            Times.Once);
    }

    [Fact]
    public void Enrich_WithoutCorrelationIdSet_DoesNotCallCreateProperty()
    {
        // Arrange
        var enricher = new CorrelationIdEnricher();
        var logEvent = new LogEvent(
            DateTime.Now,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("Test message"),
            Enumerable.Empty<LogEventProperty>());

        var propertyFactoryMock = new Mock<ILogEventPropertyFactory>();

        // Act
        enricher.Enrich(logEvent, propertyFactoryMock.Object);

        // Assert
        propertyFactoryMock.Verify(
            x => x.CreateProperty(It.IsAny<string>(), It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public void Enrich_WithEmptyCorrelationId_DoesNotCallCreateProperty()
    {
        // Arrange
        CorrelationIdHelper.SetCorrelationId(string.Empty);

        var enricher = new CorrelationIdEnricher();
        var logEvent = new LogEvent(
            DateTime.Now,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("Test message"),
            Enumerable.Empty<LogEventProperty>());

        var propertyFactoryMock = new Mock<ILogEventPropertyFactory>();

        // Act
        enricher.Enrich(logEvent, propertyFactoryMock.Object);

        // Assert
        propertyFactoryMock.Verify(
            x => x.CreateProperty(It.IsAny<string>(), It.IsAny<object>()),
            Times.Never);
    }
}
