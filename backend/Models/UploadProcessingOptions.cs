namespace CnabApi.Models;

/// <summary>
/// Configuration options for upload background processing.
/// </summary>
public class UploadProcessingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "UploadProcessing";

    /// <summary>
    /// Number of parallel workers for processing lines.
    /// Default: 4
    /// </summary>
    public int ParallelWorkers { get; set; } = 4;

    /// <summary>
    /// Number of lines between checkpoint saves.
    /// Default: 1000
    /// </summary>
    public int CheckpointInterval { get; set; } = 1000;

    /// <summary>
    /// Maximum retry attempts per line before skipping.
    /// Default: 3
    /// </summary>
    public int MaxRetryPerLine { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds between retries (uses exponential backoff).
    /// Default: 500
    /// </summary>
    public int RetryDelayMs { get; set; } = 500;
}

