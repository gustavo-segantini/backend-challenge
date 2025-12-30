namespace CnabApi.Models.Responses;

/// <summary>
/// Response model for resume upload operation.
/// </summary>
public class ResumeUploadResponse
{
    /// <summary>
    /// Success message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the upload that was resumed.
    /// </summary>
    public Guid UploadId { get; set; }

    /// <summary>
    /// Line number from which processing will resume.
    /// </summary>
    public int WillResumeFromLine { get; set; }

    /// <summary>
    /// Total number of lines in the file.
    /// </summary>
    public int TotalLineCount { get; set; }

    /// <summary>
    /// Number of lines already processed.
    /// </summary>
    public int ProcessedLineCount { get; set; }
}

