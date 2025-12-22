using CnabApi.Utilities;
using Serilog.Context;

namespace CnabApi.Middleware;

/// <summary>
/// Middleware that captures or creates a correlation ID for each request.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        const string correlationIdHeader = "X-Correlation-ID";

        var correlationId = context.Request.Headers.TryGetValue(correlationIdHeader, out var value)
            ? value.ToString()
            : Guid.NewGuid().ToString();

        CorrelationIdHelper.SetCorrelationId(correlationId);

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Append(correlationIdHeader, correlationId);

        // Add CorrelationId to the Serilog LogContext for all logs in this request scope
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
