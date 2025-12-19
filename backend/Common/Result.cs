namespace CnabApi.Common;

/// <summary>
/// Represents the result of an operation with success/failure state and error information.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    // Private constructor
    private Result(bool isSuccess, T? data, string? errorMessage = null, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
        Errors = errors ?? new List<string>();
    }

    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static Result<T> Success(T data)
        => new(true, data);

    /// <summary>
    /// Creates a failed result with error message.
    /// </summary>
    public static Result<T> Failure(string errorMessage)
        => new(false, default, errorMessage);

    /// <summary>
    /// Creates a failed result with multiple error messages.
    /// </summary>
    public static Result<T> Failure(List<string> errors)
        => new(false, default, null, errors);

    /// <summary>
    /// Creates a failed result with exception.
    /// </summary>
    public static Result<T> Failure(Exception exception)
        => new(false, default, exception.Message);

    /// <summary>
    /// Gets all error messages (single or multiple).
    /// </summary>
    public string GetErrorsAsString()
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
            return ErrorMessage;

        return string.Join("; ", Errors);
    }
}

/// <summary>
/// Represents the result of an operation without data (void operation).
/// </summary>
public class Result
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    // Private constructor
    private Result(bool isSuccess, string? errorMessage = null, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Errors = errors ?? new List<string>();
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success()
        => new(true);

    /// <summary>
    /// Creates a failed result with error message.
    /// </summary>
    public static Result Failure(string errorMessage)
        => new(false, errorMessage);

    /// <summary>
    /// Creates a failed result with multiple error messages.
    /// </summary>
    public static Result Failure(List<string> errors)
        => new(false, null, errors);

    /// <summary>
    /// Creates a failed result with exception.
    /// </summary>
    public static Result Failure(Exception exception)
        => new(false, exception.Message);

    /// <summary>
    /// Gets all error messages (single or multiple).
    /// </summary>
    public string GetErrorsAsString()
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
            return ErrorMessage;

        return string.Join("; ", Errors);
    }
}
