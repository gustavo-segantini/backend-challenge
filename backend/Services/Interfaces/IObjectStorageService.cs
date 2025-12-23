namespace CnabApi.Services;

/// <summary>
/// Interface for object storage operations (file persistence).
/// Abstracts the underlying storage implementation (MinIO, S3, etc).
/// </summary>
public interface IObjectStorageService
{
    /// <summary>
    /// Uploads a file to object storage.
    /// </summary>
    /// <param name="fileName">Name of the file to store</param>
    /// <param name="content">File content as byte array</param>
    /// <param name="contentType">MIME type of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>URL to access the uploaded file</returns>
    Task<string> UploadFileAsync(string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file from object storage.
    /// </summary>
    /// <param name="fileName">Name of the file to download</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as byte array</returns>
    Task<byte[]> DownloadFileAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists in object storage.
    /// </summary>
    /// <param name="fileName">Name of the file to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from object storage.
    /// </summary>
    /// <param name="fileName">Name of the file to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the URL to access a file in object storage.
    /// </summary>
    /// <param name="fileName">Name of the file</param>
    /// <returns>Presigned URL to access the file</returns>
    Task<string> GetFileUrlAsync(string fileName);
}
