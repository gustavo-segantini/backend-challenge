using CnabApi.Common;

namespace CnabApi.Services;

/// <summary>
/// Result extensions for file operations with specific error codes
/// </summary>
public static class FileServiceExtensions
{
    /// <summary>
    /// Enumeration of file validation error codes
    /// </summary>
    public enum FileValidationErrorCode
    {
        FileTooLarge = 413,
        UnsupportedMediaType = 415,
        InvalidContent = 422,
        FileNotProvided = 400,
        FileReadError = 500
    }

    /// <summary>
    /// Validates a file for CNAB upload with granular error codes
    /// </summary>
    public static FileValidationError? ValidateFile(IFormFile file)
    {
        // Validate file is provided
        if (file == null || file.Length == 0)
        {
            return new FileValidationError(
                FileValidationErrorCode.FileNotProvided,
                "File was not provided or is empty."
            );
        }

        const long maxFileSizeBytes = 1024 * 1024; // 1 MB
        const string allowedExtension = ".txt";

        // Validate file size
        if (file.Length > maxFileSizeBytes)
        {
            return new FileValidationError(
                FileValidationErrorCode.FileTooLarge,
                $"File exceeds the maximum allowed size of {maxFileSizeBytes / (1024 * 1024)} MB."
            );
        }

        // Validate file extension
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (fileExtension != allowedExtension)
        {
            return new FileValidationError(
                FileValidationErrorCode.UnsupportedMediaType,
                $"Only files with extension '{allowedExtension}' are allowed. File received: {fileExtension}"
            );
        }

        return null;
    }

    /// <summary>
    /// Validates file content (must contain valid CNAB lines)
    /// </summary>
    public static FileValidationError? ValidateContent(string fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return new FileValidationError(
                FileValidationErrorCode.FileNotProvided,
                "The file is empty or contains only whitespace."
            );
        }

        // Validate minimum CNAB structure (at least header and one transaction)
        var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return new FileValidationError(
                FileValidationErrorCode.InvalidContent,
                "Invalid CNAB file: must contain at least one header and one transaction record."
            );
        }

        return null;
    }
}

/// <summary>
/// Enhanced file service with granular error responses
/// </summary>
public class EnhancedFileService : IFileService
{
    private const long MaxFileSizeBytes = 1024 * 1024; // 1 MB
    private const string AllowedExtension = ".txt";

    /// <summary>
    /// Reads a CNAB file with validation returning specific error codes
    /// </summary>
    public async Task<Result<string>> ReadCnabFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate file with granular error codes
            var fileError = FileServiceExtensions.ValidateFile(file);
            if (fileError != null)
            {
                return Result<string>.Failure(fileError.Message);
            }

            // Read file content
            using var reader = new StreamReader(file!.OpenReadStream());
            var fileContent = await reader.ReadToEndAsync(cancellationToken);

            // Validate content
            var contentError = FileServiceExtensions.ValidateContent(fileContent);
            if (contentError != null)
            {
                return Result<string>.Failure(contentError.Message);
            }

            return Result<string>.Success(fileContent);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Error reading file: {ex.Message}");
        }
    }
}
