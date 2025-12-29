using CnabApi.Models;

namespace CnabApi.Services.StatusCodes;

/// <summary>
/// Factory for creating status code strategies.
/// Follows Strategy pattern for flexible status code determination.
/// </summary>
public class UploadStatusCodeStrategyFactory
{
    private readonly List<IUploadStatusCodeStrategy> _strategies;

    public UploadStatusCodeStrategyFactory()
    {
        _strategies =
        [
            new EmptyFileStatusCodeStrategy(),
            new UnsupportedMediaTypeStatusCodeStrategy(),
            new PayloadTooLargeStatusCodeStrategy(),
            new InvalidFormatStatusCodeStrategy(),
            new DefaultStatusCodeStrategy()
        ];
    }

    /// <summary>
    /// Determines the appropriate status code using the first matching strategy.
    /// </summary>
    public UploadStatusCode DetermineStatusCode(string? errorMessage)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(errorMessage))
            ?? _strategies.Last(); // Default strategy as fallback

        return strategy.GetStatusCode(errorMessage);
    }
}

/// <summary>
/// Strategy for empty file errors.
/// </summary>
public class EmptyFileStatusCodeStrategy : IUploadStatusCodeStrategy
{
    public bool CanHandle(string? errorMessage)
    {
        return !string.IsNullOrEmpty(errorMessage) &&
               errorMessage.Contains("empty", StringComparison.OrdinalIgnoreCase);
    }

    public UploadStatusCode GetStatusCode(string? errorMessage)
    {
        return UploadStatusCode.BadRequest;
    }
}

/// <summary>
/// Strategy for unsupported media type errors.
/// </summary>
public class UnsupportedMediaTypeStatusCodeStrategy : IUploadStatusCodeStrategy
{
    public bool CanHandle(string? errorMessage)
    {
        return !string.IsNullOrEmpty(errorMessage) &&
               errorMessage.Contains("not allowed", StringComparison.OrdinalIgnoreCase);
    }

    public UploadStatusCode GetStatusCode(string? errorMessage)
    {
        return UploadStatusCode.UnsupportedMediaType;
    }
}

/// <summary>
/// Strategy for file size errors.
/// </summary>
public class PayloadTooLargeStatusCodeStrategy : IUploadStatusCodeStrategy
{
    public bool CanHandle(string? errorMessage)
    {
        return !string.IsNullOrEmpty(errorMessage) &&
               errorMessage.Contains("exceeds maximum size", StringComparison.OrdinalIgnoreCase);
    }

    public UploadStatusCode GetStatusCode(string? errorMessage)
    {
        return UploadStatusCode.PayloadTooLarge;
    }
}

/// <summary>
/// Strategy for invalid format errors.
/// </summary>
public class InvalidFormatStatusCodeStrategy : IUploadStatusCodeStrategy
{
    public bool CanHandle(string? errorMessage)
    {
        return !string.IsNullOrEmpty(errorMessage) &&
               errorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase);
    }

    public UploadStatusCode GetStatusCode(string? errorMessage)
    {
        return UploadStatusCode.UnprocessableEntity;
    }
}

/// <summary>
/// Default strategy for all other errors.
/// </summary>
public class DefaultStatusCodeStrategy : IUploadStatusCodeStrategy
{
    public bool CanHandle(string? errorMessage)
    {
        return true; // Always can handle (fallback)
    }

    public UploadStatusCode GetStatusCode(string? errorMessage)
    {
        return UploadStatusCode.BadRequest;
    }
}

