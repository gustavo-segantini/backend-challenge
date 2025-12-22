using static CnabApi.Services.FileServiceExtensions;

namespace CnabApi.Services;

/// <summary>
/// Represents a file validation error with HTTP status code
/// </summary>
public class FileValidationError
{
    public FileValidationErrorCode Code { get; set; }
    public string Message { get; set; }

    public FileValidationError(FileValidationErrorCode code, string message)
    {
        Code = code;
        Message = message;
    }
}
