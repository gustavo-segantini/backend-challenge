using Microsoft.Extensions.Logging;
using Polly;

namespace CnabApi.Services.Resilience;

/// <summary>
/// Provides resilience policies using Polly for handling transient failures.
/// Includes retry policies with exponential backoff for database operations.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Retry policy for transient database exceptions with exponential backoff (2s, 4s, 8s)
    /// </summary>
    public static IAsyncPolicy<T> GetDatabaseRetryPolicy<T>(ILogger? logger = null) where T : class?
    {
        return Policy<T>
            .Handle<NullReferenceException>()
            .Or<InvalidOperationException>()
            .OrResult(r => r == null)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var correlationId = context.TryGetValue("correlationId", out var id) ? id : "N/A";
                    
                    if (outcome.Exception != null)
                    {
                        logger?.LogWarning(
                            outcome.Exception,
                            "[Resilience] Database retry attempt {RetryCount}/3. Waiting {Delay}s before next retry. CorrelationId: {CorrelationId}",
                            retryCount, timespan.TotalSeconds, correlationId);
                    }
                    else
                    {
                        logger?.LogWarning(
                            "[Resilience] Database retry attempt {RetryCount}/3 (null result). Waiting {Delay}s before next retry. CorrelationId: {CorrelationId}",
                            retryCount, timespan.TotalSeconds, correlationId);
                    }
                });
    }

    /// <summary>
    /// Retry policy for transient database exceptions (non-generic version)
    /// </summary>
    public static IAsyncPolicy GetDatabaseRetryPolicy(ILogger? logger = null)
    {
        return Policy
            .Handle<NullReferenceException>()
            .Or<InvalidOperationException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var correlationId = context.TryGetValue("correlationId", out var id) ? id : "N/A";
                    
                    logger?.LogWarning(
                        outcome,
                        "[Resilience] Database retry attempt {RetryCount}/3. Waiting {Delay}s before next retry. CorrelationId: {CorrelationId}",
                        retryCount, timespan.TotalSeconds, correlationId);
                });
    }

    /// <summary>
    /// Retry policy specifically for file operations (shorter delays)
    /// </summary>
    public static IAsyncPolicy GetFileOperationRetryPolicy(ILogger? logger = null)
    {
        return Policy
            .Handle<IOException>()
            .Or<UnauthorizedAccessException>()
            .Or<ArgumentException>()
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(500 * attempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var correlationId = context.TryGetValue("correlationId", out var id) ? id : "N/A";
                    
                    logger?.LogWarning(
                        outcome,
                        "[Resilience] File operation retry attempt {RetryCount}/2. Waiting {Delay}ms before next retry. CorrelationId: {CorrelationId}",
                        retryCount, timespan.TotalMilliseconds, correlationId);
                });
    }

    /// <summary>
    /// Circuit breaker policy to prevent cascading failures
    /// Opens after 3 failures, attempts to close after 30 seconds
    /// </summary>
    public static IAsyncPolicy<T> GetCircuitBreakerPolicy<T>(ILogger? logger = null) where T : class?
    {
        return Policy<T>
            .Handle<NullReferenceException>()
            .Or<InvalidOperationException>()
            .OrResult(r => r == null)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration, context) =>
                {
                    var correlationId = context.TryGetValue("correlationId", out var id) ? id : "N/A";
                    
                    if (outcome.Exception != null)
                    {
                        logger?.LogError(
                            outcome.Exception,
                            "[Resilience] Circuit breaker OPENED for {Duration}s after 3 failures. CorrelationId: {CorrelationId}",
                            duration.TotalSeconds, correlationId);
                    }
                    else
                    {
                        logger?.LogError(
                            "[Resilience] Circuit breaker OPENED for {Duration}s after 3 failures (null results). CorrelationId: {CorrelationId}",
                            duration.TotalSeconds, correlationId);
                    }
                },
                onReset: context =>
                {
                    var correlationId = context.TryGetValue("correlationId", out var id) ? id : "N/A";
                    logger?.LogInformation(
                        "[Resilience] Circuit breaker RESET. Service is healthy again. CorrelationId: {CorrelationId}",
                        correlationId);
                });
    }

    /// <summary>
    /// Combined policy: Retry then Circuit Breaker for maximum resilience
    /// </summary>
    public static IAsyncPolicy<T> GetCombinedDatabasePolicy<T>(ILogger? logger = null) where T : class?
    {
        var retryPolicy = GetDatabaseRetryPolicy<T>(logger);
        var circuitBreakerPolicy = GetCircuitBreakerPolicy<T>(logger);
        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    /// <summary>
    /// Timeout policy - fails after 10 seconds
    /// </summary>
    public static IAsyncPolicy GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Timeout policy with generic result type
    /// </summary>
    public static IAsyncPolicy<T> GetTimeoutPolicy<T>(TimeSpan timeout) where T : class?
    {
        return Policy.TimeoutAsync<T>(timeout);
    }
}
