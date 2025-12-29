using CnabApi.Models;

namespace CnabApi.Services.StatusCodes;

/// <summary>
/// Strategy pattern for determining upload status codes based on error messages.
/// Follows Open/Closed Principle - easy to extend with new strategies.
/// </summary>
public interface IUploadStatusCodeStrategy
{
    /// <summary>
    /// Determines if this strategy can handle the given error message.
    /// </summary>
    bool CanHandle(string? errorMessage);

    /// <summary>
    /// Gets the appropriate status code for the error message.
    /// </summary>
    UploadStatusCode GetStatusCode(string? errorMessage);
}

