using CnabApi.Common;

namespace CnabApi.Services;

/// <summary>
/// Service responsible for file operations related to CNAB file processing.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Reads a CNAB file from the uploaded form file.
    /// </summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Result containing the file content as string or error message.</returns>
    Task<Result<string>> ReadCnabFileAsync(IFormFile file, CancellationToken cancellationToken = default);
}
