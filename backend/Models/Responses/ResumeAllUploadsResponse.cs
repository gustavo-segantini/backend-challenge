namespace CnabApi.Models.Responses;

/// <summary>
/// Response model for resume all uploads operation.
/// </summary>
public class ResumeAllUploadsResponse
{
    /// <summary>
    /// Success message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Number of uploads successfully resumed.
    /// </summary>
    public int ResumedCount { get; set; }

    /// <summary>
    /// List of successfully resumed uploads.
    /// </summary>
    public IEnumerable<ResumedUploadInfo> ResumedUploads { get; set; } = new List<ResumedUploadInfo>();

    /// <summary>
    /// List of errors encountered during the operation (if any).
    /// </summary>
    public IEnumerable<string>? Errors { get; set; }
}

