using Serilog.Core;
using Serilog.Events;
using CnabApi.Utilities;

namespace CnabApi.Middleware;

/// <summary>
/// Serilog enricher that adds correlation ID to all log events.
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = CorrelationIdHelper.GetCorrelationId();
        if (!string.IsNullOrEmpty(correlationId))
        {
            var property = propertyFactory.CreateProperty("CorrelationId", correlationId);
            logEvent.AddPropertyIfAbsent(property);
        }
    }
}
