namespace CnabApi.Utilities;

/// <summary>
/// Helper class for managing correlation IDs for request tracking.
/// </summary>
public static class CorrelationIdHelper
{
    private static readonly AsyncLocal<string?> CorrelationIdStorage = new();

    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    /// <summary>
    /// Gets or creates a correlation ID for the current request context.
    /// </summary>
    public static string GetOrCreateCorrelationId()
    {
        if (string.IsNullOrEmpty(CorrelationIdStorage.Value))
        {
            CorrelationIdStorage.Value = Guid.NewGuid().ToString();
        }
        return CorrelationIdStorage.Value;
    }

    /// <summary>
    /// Sets the correlation ID for the current request context.
    /// </summary>
    public static void SetCorrelationId(string correlationId)
    {
        CorrelationIdStorage.Value = correlationId;
    }

    /// <summary>
    /// Gets the current correlation ID, or null if not set.
    /// </summary>
    public static string? GetCorrelationId()
    {
        return CorrelationIdStorage.Value;
    }
}
