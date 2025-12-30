namespace CnabApi.Models.Responses;

/// <summary>
/// Information about a resumed upload in the batch resume operation.
/// </summary>
public class ResumedUploadInfo
{
    /// <summary>
    /// The ID of the upload that was resumed.
    /// </summary>
    public Guid UploadId { get; set; }

    /// <summary>
    /// Original filename.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

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

