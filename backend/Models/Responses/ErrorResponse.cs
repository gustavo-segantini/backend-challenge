namespace CnabApi.Models.Responses;

/// <summary>
/// Standard error response model for API endpoints.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message describing what went wrong.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the ErrorResponse class.
    /// </summary>
    public ErrorResponse() { }

    /// <summary>
    /// Initializes a new instance of the ErrorResponse class with an error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    public ErrorResponse(string error)
    {
        Error = error;
    }
}

