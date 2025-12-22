using static CnabApi.Services.FileServiceExtensions;

namespace CnabApi.Services;

/// <summary>
/// Represents a file validation error with HTTP status code
/// </summary>
public class FileValidationError(FileValidationErrorCode code, string message)
{
    public FileValidationErrorCode Code { get; set; } = code;
    public string Message { get; set; } = message;
}
