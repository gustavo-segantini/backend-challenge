using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Middleware;

/// <summary>
/// Middleware that enables request body buffering to allow reading the request body multiple times.
/// This must be placed early in the middleware pipeline, before any middleware that might read the body.
/// </summary>
[ExcludeFromCodeCoverage]
public class EnableRequestBodyBufferingMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Enable buffering for all requests to allow reading Request.Body multiple times
        // This is necessary for multipart uploads where we need to read and process the body
        context.Request.EnableBuffering();
        
        await _next(context);
    }
}
