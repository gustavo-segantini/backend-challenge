using Microsoft.AspNetCore.Mvc;

namespace CnabApi.Extensions;

/// <summary>
/// Extensions for ControllerBase to provide granular HTTP status code responses
/// </summary>
public static class ControllerBaseExtensions
{
    /// <summary>
    /// Returns 413 Payload Too Large with error message
    /// </summary>
    public static ObjectResult FileTooLarge(this ControllerBase controller, string message = "File size exceeds maximum allowed (1 MB)")
    {
        return controller.StatusCode(
            StatusCodes.Status413PayloadTooLarge,
            new { error = message }
        );
    }

    /// <summary>
    /// Returns 415 Unsupported Media Type with error message
    /// </summary>
    public static ObjectResult UnsupportedMediaType(this ControllerBase controller, string message = "File type not supported. Please upload a .txt file.")
    {
        return controller.StatusCode(
            StatusCodes.Status415UnsupportedMediaType,
            new { error = message }
        );
    }

    /// <summary>
    /// Returns 422 Unprocessable Entity with error details
    /// </summary>
    public static ObjectResult UnprocessableEntity(this ControllerBase controller, string message, object? details = null)
    {
        object response = details != null 
            ? new { error = message, details } 
            : new { error = message };
        
        return controller.StatusCode(
            StatusCodes.Status422UnprocessableEntity,
            response
        );
    }

    /// <summary>
    /// Returns 500 Internal Server Error with error message (for unhandled exceptions)
    /// </summary>
    public static ObjectResult InternalServerError(this ControllerBase controller, string message = "An unexpected error occurred")
    {
        return controller.StatusCode(
            StatusCodes.Status500InternalServerError,
            new { error = message }
        );
    }

    /// <summary>
    /// Returns ProblemDetails response for RFC 7807 compliance
    /// </summary>
    public static ObjectResult Problem(this ControllerBase controller, 
        string title, 
        string detail, 
        int status, 
        string? instance = null)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = status,
            Instance = instance
        };

        return controller.StatusCode(status, problem);
    }
}
