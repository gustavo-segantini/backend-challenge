using CnabApi.Common;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Net.Http.Headers;

namespace CnabApi.Services;

/// <summary>
/// Service for handling multipart file uploads with validation.
/// Responsible for extracting and validating file content from multipart requests.
/// </summary>
public class FileUploadService : IFileUploadService
{
    private const long MaxFileSize = 1_073_741_824; // 1 GB
    private const string AllowedExtension = ".txt";
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(ILogger<FileUploadService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<string>> ReadCnabFileFromMultipartAsync(
        MultipartReader reader,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Read multipart sections
            var section = await reader.ReadNextSectionAsync(cancellationToken);
            if (section == null)
            {
                _logger.LogWarning("No file section found in multipart request");
                return Result<string>.Failure("File was not provided or is empty.");
            }

            // Step 2: Get file metadata from Content-Disposition header
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition))
            {
                _logger.LogWarning("Invalid Content-Disposition header");
                return Result<string>.Failure("Invalid file upload format");
            }

            var fileName = contentDisposition?.FileName?.ToString();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("No filename provided in Content-Disposition");
                return Result<string>.Failure("File name is required");
            }

            _logger.LogInformation("Processing uploaded file: {FileName}", fileName);

            // Step 3: Validate file extension
            if (!fileName.EndsWith(AllowedExtension, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid file extension: {FileName}", fileName);
                return Result<string>.Failure($"Only {AllowedExtension} files are allowed");
            }

            // Step 4: Read file content with size validation
            var fileContent = new StringBuilder();
            var totalBytes = 0L;

            using (var memoryStream = new MemoryStream())
            {
                await section.Body.CopyToAsync(memoryStream, cancellationToken);
                totalBytes = memoryStream.Length;

                if (totalBytes == 0)
                {
                    _logger.LogWarning("Uploaded file is empty");
                    return Result<string>.Failure("File was not provided or is empty.");
                }

                if (totalBytes > MaxFileSize)
                {
                    _logger.LogWarning("Uploaded file exceeds maximum size: {FileSize} > {MaxSize}", totalBytes, MaxFileSize);
                    return Result<string>.Failure($"File exceeds maximum size of 1GB");
                }

                // Convert file content to string
                memoryStream.Position = 0;
                using (var reader_text = new StreamReader(memoryStream, Encoding.UTF8))
                {
                    fileContent.Append(await reader_text.ReadToEndAsync(cancellationToken));
                }
            }

            _logger.LogInformation("Successfully read file: {FileName} ({FileSize} bytes)", fileName, totalBytes);
            return Result<string>.Success(fileContent.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during multipart file reading");
            return Result<string>.Failure("An unexpected error occurred while reading the file");
        }
    }
}
