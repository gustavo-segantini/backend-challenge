using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Services.ObjectStorage;

/// <summary>
/// Configuration for MinIO object storage.
/// </summary>
[ExcludeFromCodeCoverage] // Configuration class - no business logic to test
public class MinioStorageConfiguration
{
    /// <summary>
    /// MinIO server endpoint (host:port)
    /// </summary>
    public string Endpoint { get; set; } = "minio:9000";

    /// <summary>
    /// Access key for authentication
    /// </summary>
    public string AccessKey { get; set; } = "minioadmin";

    /// <summary>
    /// Secret key for authentication
    /// </summary>
    public string SecretKey { get; set; } = "minioadmin";

    /// <summary>
    /// Bucket name where files will be stored
    /// </summary>
    public string BucketName { get; set; } = "cnab-files";

    /// <summary>
    /// Whether to use SSL/TLS
    /// </summary>
    public bool UseSSL { get; set; } = false;

    /// <summary>
    /// Region where the bucket is located
    /// </summary>
    public string Region { get; set; } = "us-east-1";
}
