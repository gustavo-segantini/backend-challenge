using System.Diagnostics.CodeAnalysis;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace CnabApi.Services.ObjectStorage;

/// <summary>
/// MinIO implementation of IObjectStorageService.
/// Provides file storage operations using MinIO object storage.
/// 
/// IMPORTANT: No I/O operations in constructor - all async initialization 
/// is handled by MinioInitializationService (IHostedService).
/// </summary>
/// <remarks>
/// Constructor - lightweight, no I/O operations.
/// </remarks>
[ExcludeFromCodeCoverage] // Infrastructure code - requires MinIO integration tests
public class MinioStorageService(
    IMinioClient minioClient,
    MinioStorageConfiguration config,
    ILogger<MinioStorageService> logger) : IObjectStorageService
{
    private readonly IMinioClient _minioClient = minioClient ?? throw new ArgumentNullException(nameof(minioClient));
    private readonly MinioStorageConfiguration _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly ILogger<MinioStorageService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Uploads a file to MinIO storage.
    /// </summary>
    public async Task<string> UploadFileAsync(
        string fileName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        if (content == null || content.Length == 0)
            throw new ArgumentException("Content cannot be empty", nameof(content));

        try
        {
            _logger.LogInformation("Uploading file {FileName} to MinIO bucket {Bucket}", fileName, _config.BucketName);

            using var stream = new MemoryStream(content);

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(fileName)
                .WithStreamData(stream)
                .WithObjectSize(content.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully uploaded file {FileName} to MinIO", fileName);

            // Return the URL to access the file
            return GetFileUrl(fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to MinIO", fileName);
            throw;
        }
    }

    /// <summary>
    /// Downloads a file from MinIO storage.
    /// </summary>
    public async Task<byte[]> DownloadFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        try
        {
            _logger.LogInformation("Downloading file {FileName} from MinIO bucket {Bucket}", fileName, _config.BucketName);

            using var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(fileName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully downloaded file {FileName} from MinIO", fileName);

            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileName} from MinIO", fileName);
            throw;
        }
    }

    /// <summary>
    /// Checks if a file exists in MinIO storage.
    /// </summary>
    public async Task<bool> FileExistsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(fileName);

            await _minioClient.StatObjectAsync(statObjectArgs, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file {FileName} exists in MinIO", fileName);
            throw;
        }
    }

    /// <summary>
    /// Deletes a file from MinIO storage.
    /// </summary>
    public async Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        try
        {
            _logger.LogInformation("Deleting file {FileName} from MinIO bucket {Bucket}", fileName, _config.BucketName);

            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(fileName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully deleted file {FileName} from MinIO", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FileName} from MinIO", fileName);
            throw;
        }
    }

    /// <summary>
    /// Gets the URL to access a file in MinIO storage.
    /// </summary>
    public Task<string> GetFileUrlAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        return Task.FromResult(GetFileUrl(fileName));
    }

    /// <summary>
    /// Helper method to construct the file URL.
    /// </summary>
    private string GetFileUrl(string fileName)
    {
        var protocol = _config.UseSSL ? "https" : "http";
        var encodedFileName = Uri.EscapeDataString(fileName);
        return $"{protocol}://{_config.Endpoint}/{_config.BucketName}/{encodedFileName}";
    }
}
