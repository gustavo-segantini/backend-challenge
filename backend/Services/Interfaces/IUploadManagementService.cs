using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Models.Responses;

namespace CnabApi.Services.Interfaces;

/// <summary>
/// Service for managing file upload operations and business logic.
/// Handles upload queries, validation, and resume operations.
/// </summary>
public interface IUploadManagementService
{
    /// <summary>
    /// Gets all file uploads with pagination and optional status filter.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged response with uploads.</returns>
    Task<PagedResponse<FileUploadResponse>> GetAllUploadsAsync(
        int page = 1,
        int pageSize = 50,
        FileUploadStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds incomplete uploads that are stuck in Processing status.
    /// </summary>
    /// <param name="timeoutMinutes">Maximum minutes an upload can be in Processing status before being considered stuck.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response with incomplete uploads.</returns>
    Task<IncompleteUploadsResponse> GetIncompleteUploadsAsync(
        int timeoutMinutes = 30,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and resumes processing of a specific incomplete upload.
    /// </summary>
    /// <param name="uploadId">The ID of the upload to resume.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with resume response or error.</returns>
    Task<Result<ResumeUploadResponse>> ResumeUploadAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes processing of all incomplete uploads.
    /// </summary>
    /// <param name="timeoutMinutes">Maximum minutes an upload can be in Processing status before being considered stuck.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response with summary of resumed uploads.</returns>
    Task<ResumeAllUploadsResponse> ResumeAllIncompleteUploadsAsync(
        int timeoutMinutes = 30,
        CancellationToken cancellationToken = default);
}

