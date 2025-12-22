using CnabApi.Common;
using Microsoft.AspNetCore.WebUtilities;

namespace CnabApi.Services;

/// <summary>
/// Service responsible for handling multipart file uploads.
/// Manages reading, validating, and extracting file content from multipart requests.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Reads and validates a file from a multipart form request.
    /// </summary>
    /// <param name="reader">The MultipartReader from the HTTP request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing the file content as string, or error message if validation fails.</returns>
    Task<Result<string>> ReadCnabFileFromMultipartAsync(
        MultipartReader reader,
        CancellationToken cancellationToken = default);
}
