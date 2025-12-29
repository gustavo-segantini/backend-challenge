using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services.Interfaces;
using CnabApi.Services.StatusCodes;

namespace CnabApi.Services.Facades;

/// <summary>
/// Facade service that orchestrates transaction operations and business logic.
/// This keeps controllers thin by handling all validation, orchestration, and error mapping.
/// 
/// Integrates with object storage for persisting uploaded CNAB files.
/// Uses Redis Streams for background processing of large files with retry logic.
/// Uses Strategy pattern for status code determination (SOLID - Open/Closed Principle).
/// </summary>
public class TransactionFacadeService(
    ITransactionService transactionService,
    IFileUploadService fileUploadService,
    IFileUploadTrackingService fileUploadTrackingService,
    IObjectStorageService objectStorageService,
    IUploadQueueService uploadQueueService,
    ICnabUploadService cnabUploadService,
    IHashService hashService,
    UploadStatusCodeStrategyFactory statusCodeFactory,
    IHostEnvironment hostEnvironment,
    ILogger<TransactionFacadeService> logger) : ITransactionFacadeService
{
    private const string DefaultFileName = "uploaded-file";
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly ICnabUploadService _cnabUploadService = cnabUploadService;

    public async Task<Result<UploadResult>> UploadCnabFileAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting CNAB file upload process");

            // Extract and validate multipart boundary
            var boundary = GetMultipartBoundary(request);
            if (string.IsNullOrEmpty(boundary))
            {
                return Result<UploadResult>.Failure("Invalid multipart/form-data request.");
            }

            var reader = new MultipartReader(boundary, request.Body);

            // Use FileUploadService to read and validate the file
            var fileResult = await fileUploadService.ReadCnabFileFromMultipartAsync(reader, cancellationToken);

            if (!fileResult.IsSuccess)
            {
                logger.LogWarning("File upload validation failed. Error: {Error}", fileResult.ErrorMessage);
                
                var statusCode = statusCodeFactory.DetermineStatusCode(fileResult.ErrorMessage);
                var uploadResult = CreateFailureUploadResult(statusCode);
                return Result<UploadResult>.Failure(fileResult.ErrorMessage ?? "File upload validation failed", uploadResult);
            }

            // Check for duplicates and calculate file metadata (hash/size) in one pass
            var (duplicateCheckResult, fileHash, fileSize) = await CheckForDuplicateUploadAsync(fileResult.Data!, cancellationToken);
            if (duplicateCheckResult != null)
            {
                return duplicateCheckResult;
            }

            // Phase 1: Store file in MinIO FIRST (before creating record or enqueueing)
            string? storagePath;
            try
            {
                storagePath = await StoreFileInObjectStorageAsync(fileResult.Data!, cancellationToken);
                if (string.IsNullOrEmpty(storagePath))
                {
                    logger.LogWarning("File storage in MinIO failed, but continuing with upload. File will be processed without storage backup.");
                    // Continue without storage path - this allows uploads to work even when MinIO is unavailable
                }
                else
                {
                    logger.LogInformation(
                        "File stored in MinIO. StoragePath: {StoragePath}, FileSize: {FileSize} bytes",
                        storagePath, fileSize);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "File storage in MinIO failed, but continuing with upload. File will be processed without storage backup.");
                storagePath = null; // Continue without storage path
            }

            // Phase 2: Create FileUpload record with Pending status and storage path
            var fileUploadRecord = await fileUploadTrackingService.RecordPendingUploadAsync(
                DefaultFileName,
                fileHash,
                fileSize,
                storagePath ?? string.Empty, // Can be null if MinIO storage failed
                cancellationToken);

            logger.LogInformation(
                "File upload recorded as pending. UploadId: {UploadId}, FileName: {FileName}, StoragePath: {StoragePath}",
                fileUploadRecord.Id, DefaultFileName, storagePath);

            // Phase 3: Process synchronously in test environment, otherwise enqueue for background processing
            if (_hostEnvironment.IsEnvironment("Test"))
            {
                // For tests, process synchronously
                try
                {
                    var processResult = await _cnabUploadService.ProcessCnabUploadAsync(
                        fileResult.Data!,
                        fileUploadRecord.Id,
                        0,
                        cancellationToken);

                    if (processResult.IsSuccess)
                    {
                        // Update as completed
                        await fileUploadTrackingService.UpdateProcessingSuccessAsync(
                            fileUploadRecord.Id,
                            processResult.Data,
                            storagePath,
                            cancellationToken);

                        return Result<UploadResult>.Success(new UploadResult
                        {
                            TransactionCount = processResult.Data,
                            StatusCode = UploadStatusCode.Success,
                            UploadId = fileUploadRecord.Id
                        });
                    }
                    else
                    {
                        // Processing failed - likely due to invalid content
                        await fileUploadTrackingService.UpdateProcessingFailureAsync(
                            fileUploadRecord.Id,
                            processResult.ErrorMessage ?? "Processing failed",
                            0,
                            cancellationToken);

                        return Result<UploadResult>.Failure(
                            processResult.ErrorMessage ?? "Processing failed",
                            CreateFailureUploadResult(UploadStatusCode.UnprocessableEntity));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process file synchronously. UploadId: {UploadId}", fileUploadRecord.Id);
                    
                    await fileUploadTrackingService.UpdateProcessingFailureAsync(
                        fileUploadRecord.Id,
                        $"Failed to process synchronously: {ex.Message}",
                        0,
                        cancellationToken);

                    return Result<UploadResult>.Failure(
                        "Failed to process file",
                        CreateFailureUploadResult(UploadStatusCode.InternalServerError));
                }
            }
            else
            {
                // Production: Enqueue for background processing
                try
                {
                    var messageId = await uploadQueueService.EnqueueUploadAsync(
                        fileUploadRecord.Id,
                        storagePath,
                        cancellationToken);

                    logger.LogInformation(
                        "File enqueued for background processing. UploadId: {UploadId}, MessageId: {MessageId}",
                        fileUploadRecord.Id, messageId);

                    // Return 202 Accepted indicating the file has been queued for processing
                    return Result<UploadResult>.Success(new UploadResult
                    {
                        TransactionCount = 0, // Processing not complete yet
                        StatusCode = UploadStatusCode.Accepted, // 202 Accepted
                        UploadId = fileUploadRecord.Id
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to enqueue file for background processing. UploadId: {UploadId}", fileUploadRecord.Id);
                    
                    // Update FileUpload record as failed
                    await fileUploadTrackingService.UpdateProcessingFailureAsync(
                        fileUploadRecord.Id,
                        $"Failed to enqueue for processing: {ex.Message}",
                        0,
                        cancellationToken);

                    return Result<UploadResult>.Failure(
                        "Failed to queue file for processing",
                        CreateFailureUploadResult(UploadStatusCode.InternalServerError));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during CNAB file upload");
            return Result<UploadResult>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }


    public async Task<Result> ClearAllDataAsync(CancellationToken cancellationToken)
    {
        logger.LogWarning("Admin initiated data clear operation");

        try
        {
            var result = await transactionService.ClearAllDataAsync(cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogError("Failed to clear data. Error: {Error}", result.ErrorMessage);
                return Result.Failure(result.ErrorMessage ?? "Failed to clear data");
            }

            logger.LogInformation("Data cleared successfully");

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during data clear operation");
            return Result.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }

    public async Task<Result<List<StoreGroupedTransactions>>> GetTransactionsGroupedByStoreAsync(
        Guid? uploadId = null,
        CancellationToken cancellationToken = default)
    {
        if (uploadId.HasValue)
        {
            logger.LogInformation("Fetching transactions grouped by store for upload {UploadId}", uploadId.Value);
        }
        else
        {
            logger.LogInformation("Fetching all transactions grouped by store");
        }
        var result = await transactionService.GetTransactionsGroupedByStoreAsync(uploadId, cancellationToken);
        return result;
    }

    /// <summary>
    /// Extracts the boundary from a multipart/form-data Content-Type header.
    /// </summary>
    private static string? GetMultipartBoundary(HttpRequest request)
    {
        var contentType = request.ContentType;
        if (string.IsNullOrEmpty(contentType))
        {
            return null;
        }

        var elements = contentType.Split(';');
        var boundaryElement = elements.FirstOrDefault(e => e.Trim().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase));

        if (boundaryElement == null)
        {
            return null;
        }

        var boundary = boundaryElement.Split('=', 2)[1].Trim();
        // Remove quotes if present
        if (boundary.StartsWith('\"') && boundary.EndsWith('\"'))
        {
            boundary = boundary[1..^1];
        }

        return boundary;
    }


    /// <summary>
    /// Handles CNAB processing failures by recording them in the file upload tracking service.
    /// </summary>
    /// <param name="fileUploadId">The file upload ID</param>
    /// <param name="errorMessage">The error message from the upload service</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task HandleCnabProcessingFailureAsync(Guid fileUploadId, string? errorMessage, CancellationToken cancellationToken)
    {
        logger.LogWarning("CNAB file upload processing failed. FileUploadId: {FileUploadId}, Error: {Error}", fileUploadId, errorMessage);
        
        try
        {
            // In a real scenario, you'd update the existing FileUpload record
            // For now, we just log it since the record is already created
            // If needed in future, add an UpdateFileUploadStatusAsync method to IFileUploadTrackingService
            logger.LogInformation("FileUpload record with ID {FileUploadId} should be marked as failed", fileUploadId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling CNAB processing failure for FileUploadId: {FileUploadId}", fileUploadId);
        }
    }

    /// <summary>
    /// Checks if the uploaded file is a duplicate based on SHA256 hash.
    /// Also returns the calculated hash and file size for reuse in subsequent operations.
    /// </summary>
    /// <param name="fileContent">The file content to check for duplicates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing: duplicate result (null if unique), file hash, and file size</returns>
    private async Task<(Result<UploadResult>? duplicateResult, string fileHash, long fileSize)> CheckForDuplicateUploadAsync(
        string fileContent, 
        CancellationToken cancellationToken)
    {
        // Calculate file hash and size once for reuse (DRY: using HashService)
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
        var fileHash = hashService.ComputeFileHash(fileContent); // Use HashService directly
        var fileSize = fileBytes.LongLength;
        
        var (isUnique, existingUpload) = await fileUploadTrackingService.IsFileUniqueAsync(fileHash, cancellationToken);
        if (!isUnique && existingUpload != null)
        {
            logger.LogWarning(
                "Duplicate file upload rejected. File: {FileName}, Previous upload: {PreviousUpload} ({ProcessedLines} lines)",
                DefaultFileName,
                existingUpload.FileName,
                existingUpload.ProcessedLineCount);

            var duplicateResult = Result<UploadResult>.Failure(
                "Este arquivo já foi processado anteriormente. Para evitar duplicatas, o upload foi rejeitado.",
                CreateDuplicateUploadResult(existingUpload));
            
            return (duplicateResult, fileHash, fileSize);
        }

        return (null, fileHash, fileSize);
    }

    /// <summary>
    /// Stores the CNAB file content in object storage for audit trail.
    /// Failures are logged but do not affect the upload operation.
    /// </summary>
    /// <param name="fileContent">The file content to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Storage path or null if storage failed</returns>
    private async Task<string?> StoreFileInObjectStorageAsync(string fileContent, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = $"cnab-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.txt";
            var fileBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
            var fileUrl = await objectStorageService.UploadFileAsync(
                fileName,
                fileBytes,
                "text/plain",
                cancellationToken);

            logger.LogInformation("CNAB file stored in object storage: {FileUrl}", fileUrl);
            return fileName;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store CNAB file in object storage. Continuing with upload.");
            // Don't fail the upload if storage fails - the transaction data is already saved
            return null;
        }
    }

    /// <summary>
    /// Creates an upload result for duplicate file uploads.
    /// </summary>
    /// <param name="existingUpload">The previously uploaded file record</param>
    /// <returns>Upload result with duplicate status and 409 conflict code</returns>
    private static UploadResult CreateDuplicateUploadResult(FileUpload existingUpload)
    {
        return new UploadResult
        {
            TransactionCount = 0,
            StatusCode = UploadStatusCode.Conflict // File already processed
        };
    }

    /// <summary>
    /// Creates an UploadResult representing a failure with zero transactions.
    /// </summary>
    /// <param name="statusCode">The status code indicating the type of failure</param>
    /// <returns>UploadResult with zero transactions and the specified status code</returns>
    private static UploadResult CreateFailureUploadResult(UploadStatusCode statusCode)
    {
        return new UploadResult
        {
            TransactionCount = 0,
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Parses a comma-separated string into a list of trimmed values.
    /// Returns empty list if input is null or whitespace.
    /// </summary>
    /// <param name="commaSeparatedValues">Comma-separated string to parse</param>
    /// <returns>List of trimmed non-empty values</returns>
    private static List<string> ParseCommaSeparatedList(string? commaSeparatedValues)
    {
        return string.IsNullOrWhiteSpace(commaSeparatedValues)
            ? []
            : commaSeparatedValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
