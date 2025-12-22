using CnabApi.Common;

namespace CnabApi.Services;

/// <summary>
/// Implementation of file service for CNAB file operations.
/// </summary>
public class FileService : IFileService
{
    private const long MaxFileSizeBytes = 1024 * 1024; // 1 MB
    private const string AllowedExtension = ".txt";

    /// <summary>
    /// Reads a CNAB file from the uploaded form file.
    /// Validates file size, extension and content.
    /// </summary>
    public async Task<Result<string>> ReadCnabFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate file is provided
            if (file == null || file.Length == 0)
                return Result<string>.Failure("File was not provided or is empty.");

            // Validate file size
            if (file.Length > MaxFileSizeBytes)
                return Result<string>.Failure($"File exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

            // Validate file extension
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (fileExtension != AllowedExtension)
                return Result<string>.Failure($"Only files with extension '{AllowedExtension}' are allowed.");

            // Read file content
            using var reader = new StreamReader(file.OpenReadStream());
            var fileContent = await reader.ReadToEndAsync(cancellationToken);

            // Validate content is not empty
            if (string.IsNullOrWhiteSpace(fileContent))
                return Result<string>.Failure("The file is empty or contains only whitespace.");

            return Result<string>.Success(fileContent);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Error reading file: {ex.Message}");
        }
    }
}
