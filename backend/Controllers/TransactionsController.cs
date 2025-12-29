using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CnabApi.Models;
using CnabApi.Services.Facades;
using CnabApi.Services;
using CnabApi.Services.Interfaces;
using CnabApi.Extensions;

namespace CnabApi.Controllers;

/// <summary>
/// API controller for managing CNAB file uploads and transaction queries.
/// 
/// This controller handles HTTP request/response mapping and delegates
/// all business logic to the TransactionFacadeService.
/// 
/// Responsibilities:
/// - CNAB file uploads and parsing
/// - Transaction queries grouped by store for specific file uploads
/// - Admin operations for data management
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
[Tags("Transactions")]
public class TransactionsController(
    ITransactionFacadeService facadeService,
    IFileUploadTrackingService fileUploadTrackingService,
    IUploadQueueService uploadQueueService,
    ILogger<TransactionsController> logger) : ControllerBase
{
    private readonly ITransactionFacadeService _facadeService = facadeService;
    private readonly IFileUploadTrackingService _fileUploadTrackingService = fileUploadTrackingService;
    private readonly IUploadQueueService _uploadQueueService = uploadQueueService;
    private readonly ILogger<TransactionsController> _logger = logger;

    /// <summary>
    /// Uploads a CNAB file, parses transactions, and stores them in the database.
    /// 
    /// Expects a CNAB-formatted text file with fixed-width fields per transaction line.
    /// Each line is parsed and validated against CNAB specifications.
    /// Uses streaming approach with MultipartReader for efficient memory usage.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Success message with transaction count or error details.</returns>
    /// <remarks>
    /// **Sample Request:**
    /// ```
    /// POST /api/v1/transactions/upload
    /// Authorization: Bearer {token}
    /// Content-Type: multipart/form-data
    /// 
    /// file: [CNAB format text file, up to 1GB]
    /// ```
    /// 
    /// **Sample Response (200):**
    /// ```json
    /// {
    ///   "message": "Successfully imported 201 transactions",
    ///   "count": 201
    /// }
    /// ```
    /// 
    /// **Error Cases:**
    /// - 400: File is empty, invalid format, or contains malformed records
    /// - 401: Missing or invalid authentication token
    /// - 413: File too large (exceeds 1GB)
    /// - 415: Unsupported media type
    /// - 422: Invalid CNAB content
    /// </remarks>
    [HttpPost("upload")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(object), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadCnabFile(CancellationToken cancellationToken)
    {
        var result = await _facadeService.UploadCnabFileAsync(Request, cancellationToken);

        if (!result.IsSuccess)
        {
            var errorResponse = new { error = result.ErrorMessage };
            
            return result.Data?.StatusCode switch
            {
                UploadStatusCode.BadRequest => BadRequest(errorResponse),
                UploadStatusCode.UnsupportedMediaType => this.UnsupportedMediaType(result.ErrorMessage!),
                UploadStatusCode.PayloadTooLarge => this.FileTooLarge(result.ErrorMessage!),
                UploadStatusCode.UnprocessableEntity => this.UnprocessableEntity(result.ErrorMessage!),
                _ => BadRequest(errorResponse)
            };
        }

        // Return 202 Accepted for background processing
        if (result.Data?.StatusCode == UploadStatusCode.Accepted)
        {
            return Accepted(new
            {
                message = "File accepted and queued for background processing",
                status = "processing"
            });
        }

        // Return 200 OK for synchronous processing (e.g., tests)
        if (result.Data?.StatusCode == UploadStatusCode.Success)
        {
            return Ok(new
            {
                message = $"Successfully imported {result.Data.TransactionCount} transactions",
                count = result.Data.TransactionCount
            });
        }

        return Ok(new
        {
            message = $"Successfully imported {result.Data!.TransactionCount} transactions",
            count = result.Data.TransactionCount
        });
    }


    /// <summary>
    /// Gets all file uploads with pagination and optional status filter.
    /// Available to all authenticated users for monitoring upload status and progress.
    /// </summary>
    /// <param name="page">Page number (1-based). Default: 1.</param>
    /// <param name="pageSize">Items per page (1-100). Default: 50.</param>
    /// <param name="status">Optional status filter (Pending, Processing, Success, Failed, Duplicate, PartiallyCompleted).</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Paged list of uploads with detailed information.</returns>
    /// <remarks>
    /// **Sample Request:**
    /// ```
    /// GET /api/v1/transactions/uploads?page=1&amp;pageSize=20&amp;status=Processing
    /// Authorization: Bearer {token}
    /// ```
    /// </remarks>
    [HttpGet("uploads")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllUploads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        FileUploadStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<FileUploadStatus>(status, true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var (uploads, totalCount) = await _fileUploadTrackingService.GetAllUploadsAsync(
            page, pageSize, statusFilter, cancellationToken);

        var result = uploads.Select(u => new
        {
            id = u.Id,
            fileName = u.FileName,
            status = u.Status.ToString(),
            fileSize = u.FileSize,
            totalLineCount = u.TotalLineCount,
            processedLineCount = u.ProcessedLineCount,
            failedLineCount = u.FailedLineCount,
            skippedLineCount = u.SkippedLineCount,
            lastCheckpointLine = u.LastCheckpointLine,
            lastCheckpointAt = u.LastCheckpointAt,
            processingStartedAt = u.ProcessingStartedAt,
            processingCompletedAt = u.ProcessingCompletedAt,
            uploadedAt = u.UploadedAt,
            retryCount = u.RetryCount,
            errorMessage = u.ErrorMessage,
            storagePath = u.StoragePath,
            progressPercentage = u.TotalLineCount > 0 
                ? Math.Round((double)(u.ProcessedLineCount + u.FailedLineCount + u.SkippedLineCount) / u.TotalLineCount * 100, 2)
                : 0
        }).ToList();

        return Ok(new
        {
            items = result,
            totalCount = totalCount,
            page = page,
            pageSize = pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Lists incomplete uploads that are stuck in Processing status.
    /// Useful for detecting uploads that were interrupted and need to be resumed.
    /// Available to all authenticated users.
    /// </summary>
    /// <param name="timeoutMinutes">Maximum minutes an upload can be in Processing status before being considered stuck. Default: 30.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>List of incomplete uploads with their checkpoint information.</returns>
    /// <remarks>
    /// **Sample Request:**
    /// ```
    /// GET /api/v1/transactions/uploads/incomplete?timeoutMinutes=30
    /// Authorization: Bearer {token}
    /// ```
    /// 
    /// **Sample Response (200):**
    /// ```json
    /// {
    ///   "incompleteUploads": [
    ///     {
    ///       "id": "123e4567-e89b-12d3-a456-426614174000",
    ///       "fileName": "cnab.txt",
    ///       "status": "Processing",
    ///       "totalLineCount": 1000,
    ///       "processedLineCount": 450,
    ///       "lastCheckpointLine": 450,
    ///       "lastCheckpointAt": "2025-12-26T10:30:00Z",
    ///       "processingStartedAt": "2025-12-26T10:00:00Z"
    ///     }
    ///   ],
    ///   "count": 1
    /// }
    /// ```
    /// </remarks>
    [HttpGet("uploads/incomplete")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetIncompleteUploads(
        [FromQuery] int timeoutMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        var incompleteUploads = await _fileUploadTrackingService.FindIncompleteUploadsAsync(timeoutMinutes, cancellationToken);

        var result = incompleteUploads.Select(u => new
        {
            id = u.Id,
            fileName = u.FileName,
            status = u.Status.ToString(),
            totalLineCount = u.TotalLineCount,
            processedLineCount = u.ProcessedLineCount,
            failedLineCount = u.FailedLineCount,
            skippedLineCount = u.SkippedLineCount,
            lastCheckpointLine = u.LastCheckpointLine,
            lastCheckpointAt = u.LastCheckpointAt,
            processingStartedAt = u.ProcessingStartedAt,
            uploadedAt = u.UploadedAt,
            storagePath = u.StoragePath
        }).ToList();

        return Ok(new
        {
            incompleteUploads = result,
            count = result.Count
        });
    }

    /// <summary>
    /// Resumes processing of a specific incomplete upload.
    /// Re-enqueues the upload for background processing, starting from the last checkpoint.
    /// </summary>
    /// <param name="uploadId">The ID of the upload to resume.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Success message with upload information.</returns>
    /// <remarks>
    /// **Sample Request:**
    /// ```
    /// POST /api/v1/transactions/uploads/{uploadId}/resume
    /// Authorization: Bearer {token}
    /// ```
    /// 
    /// **Sample Response (200):**
    /// ```json
    /// {
    ///   "message": "Upload re-enqueued for processing",
    ///   "uploadId": "123e4567-e89b-12d3-a456-426614174000",
    ///   "willResumeFromLine": 450
    /// }
    /// ```
    /// 
    /// **Error Cases:**
    /// - 404: Upload not found
    /// - 400: Upload is not incomplete or cannot be resumed
    /// </remarks>
    [HttpPost("uploads/{uploadId}/resume")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResumeUpload(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var upload = await _fileUploadTrackingService.GetUploadByIdAsync(uploadId, cancellationToken);

        if (upload == null)
        {
            return NotFound(new { error = $"Upload with ID {uploadId} not found" });
        }

        var isIncomplete = await _fileUploadTrackingService.IsUploadIncompleteAsync(uploadId, cancellationToken);

        if (!isIncomplete)
        {
            return BadRequest(new { error = "Upload is not incomplete or cannot be resumed" });
        }

        if (string.IsNullOrEmpty(upload.StoragePath))
        {
            return BadRequest(new { error = "Upload does not have a storage path and cannot be resumed" });
        }

        // Re-enqueue the upload for processing
        await _uploadQueueService.EnqueueUploadAsync(uploadId, upload.StoragePath, cancellationToken);

        _logger.LogInformation(
            "Upload re-enqueued for processing. UploadId: {UploadId}, WillResumeFromLine: {LastCheckpointLine}",
            uploadId, upload.LastCheckpointLine);

        return Ok(new
        {
            message = "Upload re-enqueued for processing",
            uploadId = uploadId,
            willResumeFromLine = upload.LastCheckpointLine,
            totalLineCount = upload.TotalLineCount,
            processedLineCount = upload.ProcessedLineCount
        });
    }

    /// <summary>
    /// Resumes processing of all incomplete uploads.
    /// Finds all uploads stuck in Processing status and re-enqueues them for background processing.
    /// </summary>
    /// <param name="timeoutMinutes">Maximum minutes an upload can be in Processing status before being considered stuck. Default: 30.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Summary of resumed uploads.</returns>
    /// <remarks>
    /// **Sample Request:**
    /// ```
    /// POST /api/v1/transactions/uploads/resume-all?timeoutMinutes=30
    /// Authorization: Bearer {token}
    /// ```
    /// 
    /// **Sample Response (200):**
    /// ```json
    /// {
    ///   "message": "Resumed 3 incomplete uploads",
    ///   "resumedCount": 3,
    ///   "resumedUploads": [
    ///     {
    ///       "uploadId": "123e4567-e89b-12d3-a456-426614174000",
    ///       "willResumeFromLine": 450
    ///     }
    ///   ]
    /// }
    /// ```
    /// </remarks>
    [HttpPost("uploads/resume-all")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResumeAllIncompleteUploads(
        [FromQuery] int timeoutMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        var incompleteUploads = await _fileUploadTrackingService.FindIncompleteUploadsAsync(timeoutMinutes, cancellationToken);

        var resumedUploads = new List<object>();
        var errors = new List<string>();

        foreach (var upload in incompleteUploads)
        {
            try
            {
                if (string.IsNullOrEmpty(upload.StoragePath))
                {
                    errors.Add($"Upload {upload.Id} does not have a storage path");
                    continue;
                }

                await _uploadQueueService.EnqueueUploadAsync(upload.Id, upload.StoragePath, cancellationToken);

                resumedUploads.Add(new
                {
                    uploadId = upload.Id,
                    fileName = upload.FileName,
                    willResumeFromLine = upload.LastCheckpointLine,
                    totalLineCount = upload.TotalLineCount,
                    processedLineCount = upload.ProcessedLineCount
                });

                _logger.LogInformation(
                    "Upload re-enqueued for processing. UploadId: {UploadId}, WillResumeFromLine: {LastCheckpointLine}",
                    upload.Id, upload.LastCheckpointLine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming upload. UploadId: {UploadId}", upload.Id);
                errors.Add($"Error resuming upload {upload.Id}: {ex.Message}");
            }
        }

        return Ok(new
        {
            message = $"Resumed {resumedUploads.Count} incomplete upload(s)",
            resumedCount = resumedUploads.Count,
            resumedUploads = resumedUploads,
            errors = errors.Count > 0 ? errors : null
        });
    }

    /// <summary>
    /// Gets transactions grouped by store name and owner for a specific file upload, with balance calculated for each store.
    /// 
    /// Returns transactions organized by store from a specific uploaded file, making it easy to see all transactions
    /// and the balance for each store in that file.
    /// </summary>
    /// <param name="uploadId">The ID of the file upload to get transactions from.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>List of stores with their transactions and balances.</returns>
    /// <remarks>
    /// **Sample Request:**
    /// ```
    /// GET /api/v1/transactions/stores/{uploadId}
    /// Authorization: Bearer {token}
    /// ```
    /// 
    /// **Sample Response (200):**
    /// ```json
    /// [
    ///   {
    ///     "storeName": "BAR DO JOÃO",
    ///     "storeOwner": "096.206.760-17",
    ///     "transactions": [
    ///       {
    ///         "id": 1,
    ///         "storeName": "BAR DO JOÃO",
    ///         "storeOwner": "096.206.760-17",
    ///         "transactionDate": "2019-03-01T00:00:00Z",
    ///         "transactionTime": "15:34:53",
    ///         "amount": 142.00,
    ///         "natureCode": "3"
    ///       }
    ///     ],
    ///     "balance": -102.00
    ///   }
    /// ]
    /// ```
    /// </remarks>
    [HttpGet("stores/{uploadId}")]
    [Authorize]
    [ProducesResponseType(typeof(List<StoreGroupedTransactions>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<StoreGroupedTransactions>>> GetTransactionsGroupedByStore(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var result = await _facadeService.GetTransactionsGroupedByStoreAsync(uploadId, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        if (result.Data == null || result.Data.Count == 0)
        {
            return NotFound(new { error = "No transactions found for this upload" });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Clears all transactions and stores from the database.
    /// WARNING: This operation cannot be undone.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Success message.</returns>
    [HttpDelete]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearData(CancellationToken cancellationToken)
    {
        var result = await _facadeService.ClearAllDataAsync(cancellationToken);

        return result.IsSuccess
            ? Ok(new { message = "All data cleared successfully" })
            : BadRequest(new { error = result.ErrorMessage });
    }
}
