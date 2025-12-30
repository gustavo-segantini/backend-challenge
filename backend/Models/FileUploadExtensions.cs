using CnabApi.Models.Responses;

namespace CnabApi.Models;

/// <summary>
/// Extension methods for FileUpload entity to map to response DTOs.
/// </summary>
public static class FileUploadExtensions
{
    /// <summary>
    /// Maps a FileUpload entity to a FileUploadResponse DTO.
    /// </summary>
    /// <param name="upload">The FileUpload entity to map.</param>
    /// <returns>A FileUploadResponse DTO.</returns>
    public static FileUploadResponse ToResponse(this FileUpload upload)
    {
        return new FileUploadResponse
        {
            Id = upload.Id,
            FileName = upload.FileName,
            Status = upload.Status.ToString(),
            FileSize = upload.FileSize,
            TotalLineCount = upload.TotalLineCount,
            ProcessedLineCount = upload.ProcessedLineCount,
            FailedLineCount = upload.FailedLineCount,
            SkippedLineCount = upload.SkippedLineCount,
            LastCheckpointLine = upload.LastCheckpointLine,
            LastCheckpointAt = upload.LastCheckpointAt,
            ProcessingStartedAt = upload.ProcessingStartedAt,
            ProcessingCompletedAt = upload.ProcessingCompletedAt,
            UploadedAt = upload.UploadedAt,
            RetryCount = upload.RetryCount,
            ErrorMessage = upload.ErrorMessage,
            StoragePath = upload.StoragePath,
            ProgressPercentage = upload.TotalLineCount > 0
                ? Math.Round((double)(upload.ProcessedLineCount + upload.FailedLineCount + upload.SkippedLineCount) / upload.TotalLineCount * 100, 2)
                : 0
        };
    }

    /// <summary>
    /// Maps a collection of FileUpload entities to FileUploadResponse DTOs.
    /// </summary>
    /// <param name="uploads">The collection of FileUpload entities to map.</param>
    /// <returns>A collection of FileUploadResponse DTOs.</returns>
    public static IEnumerable<FileUploadResponse> ToResponse(this IEnumerable<FileUpload> uploads)
    {
        return uploads.Select(u => u.ToResponse());
    }
}

