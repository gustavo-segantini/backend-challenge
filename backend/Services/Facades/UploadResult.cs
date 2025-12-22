namespace CnabApi.Services;

/// <summary>
/// Result model for file upload operations with status code hints.
/// </summary>
public class UploadResult
{
    public int TransactionCount { get; set; }
    public UploadStatusCode StatusCode { get; set; }
}
