using CnabApi.Utilities;

namespace CnabApi.Middleware;

/// <summary>
/// Middleware that captures or creates a correlation ID for each request.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        const string correlationIdHeader = "X-Correlation-ID";

        var correlationId = context.Request.Headers.TryGetValue(correlationIdHeader, out var value)
            ? value.ToString()
            : Guid.NewGuid().ToString();

        CorrelationIdHelper.SetCorrelationId(correlationId);

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers.Append(correlationIdHeader, correlationId);

        await _next(context);
    }
}
