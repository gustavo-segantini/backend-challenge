namespace CnabApi.Models.Responses;

/// <summary>
/// Response model for incomplete uploads endpoint.
/// </summary>
public class IncompleteUploadsResponse
{
    /// <summary>
    /// Gets or sets the list of incomplete uploads.
    /// </summary>
    public IEnumerable<FileUploadResponse> IncompleteUploads { get; set; } = new List<FileUploadResponse>();

    /// <summary>
    /// Gets or sets the count of incomplete uploads.
    /// </summary>
    public int Count { get; set; }
}

