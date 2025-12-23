using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace CnabApi.Services.ObjectStorage;

/// <summary>
/// Hosted service for async MinIO bucket initialization.
/// Ensures the bucket exists before the application starts serving requests.
/// 
/// This avoids blocking I/O during dependency injection setup and follows
/// best practices for async initialization in ASP.NET Core.
/// </summary>
public class MinioInitializationService(
    IMinioClient minioClient,
    MinioStorageConfiguration config,
    ILogger<MinioInitializationService> logger) : IHostedService
{
    private readonly IMinioClient _minioClient = minioClient ?? throw new ArgumentNullException(nameof(minioClient));
    private readonly MinioStorageConfiguration _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly ILogger<MinioInitializationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Starts async initialization - checks/creates MinIO bucket.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting MinIO initialization for bucket: {BucketName}", _config.BucketName);

            // Check if bucket exists
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(_config.BucketName);

            var bucketExists = await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken)
                .ConfigureAwait(false);

            if (!bucketExists)
            {
                _logger.LogInformation("Bucket {BucketName} does not exist. Creating...", _config.BucketName);

                // Create the bucket
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_config.BucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation("Successfully created bucket: {BucketName}", _config.BucketName);
            }
            else
            {
                _logger.LogInformation("Bucket {BucketName} already exists", _config.BucketName);
            }

            _logger.LogInformation("MinIO initialization completed successfully");
        }
        catch (HttpRequestException ex)
        {
            // MinIO might not be ready yet (container starting up)
            _logger.LogWarning(ex, "Could not connect to MinIO at {Endpoint}. MinIO may still be starting up. The application will continue in degraded mode.", _config.Endpoint);
        }
        catch (AccessDeniedException ex)
        {
            // User doesn't have permission to access the bucket or create it
            // This is non-fatal - the application continues in degraded mode
            _logger.LogWarning(ex, "Access denied to MinIO bucket {Bucket}. Check user credentials and permissions. The application will continue in degraded mode.", _config.BucketName);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the application startup
            _logger.LogError(ex, "Error during MinIO initialization. The application will continue in degraded mode.");
        }
    }

    /// <summary>
    /// Cleanup on shutdown (if needed).
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MinIO initialization service stopping");
        return Task.CompletedTask;
    }
}
