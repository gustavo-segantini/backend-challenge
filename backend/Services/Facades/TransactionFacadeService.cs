using Microsoft.AspNetCore.WebUtilities;
using CnabApi.Common;
using CnabApi.Models;

namespace CnabApi.Services.Facades;

/// <summary>
/// Facade service that orchestrates transaction operations and business logic.
/// This keeps controllers thin by handling all validation, orchestration, and error mapping.
/// 
/// Integrates with object storage for persisting uploaded CNAB files.
/// </summary>
public class TransactionFacadeService(
    ICnabUploadService uploadService,
    ITransactionService transactionService,
    IFileUploadService fileUploadService,
    IObjectStorageService objectStorageService,
    ILogger<TransactionFacadeService> logger) : ITransactionFacadeService
{
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
                
                var statusCode = DetermineUploadStatusCode(fileResult.ErrorMessage);
                var uploadResult = CreateFailureUploadResult(statusCode);
                return Result<UploadResult>.Failure(fileResult.ErrorMessage ?? "File upload validation failed", uploadResult);
            }

            // Process the uploaded file content
            var result = await uploadService.ProcessCnabUploadAsync(fileResult.Data!, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("CNAB file upload failed. Error: {Error}", result.ErrorMessage);
                
                // Check if it's a content validation error (422) or general error (400)
                var statusCode = result.ErrorMessage?.Contains("invalid", StringComparison.OrdinalIgnoreCase) ?? false
                    ? UploadStatusCode.UnprocessableEntity
                    : UploadStatusCode.BadRequest;
                
                var uploadResult = CreateFailureUploadResult(statusCode);
                return Result<UploadResult>.Failure(result.ErrorMessage ?? "Unknown error during upload", uploadResult);
            }

            // Store the original file in MinIO for audit trail
            await StoreFileInObjectStorageAsync(fileResult.Data!, cancellationToken);

            logger.LogInformation("CNAB file upload completed successfully. Transactions imported: {Count}", result.Data);

            return Result<UploadResult>.Success(new UploadResult
            {
                TransactionCount = result.Data,
                StatusCode = UploadStatusCode.Success
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during CNAB file upload");
            return Result<UploadResult>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }

    public async Task<Result<PagedResult<Transaction>>> GetTransactionsByCpfAsync(
        string cpf,
        int page,
        int pageSize,
        DateTime? startDate,
        DateTime? endDate,
        string? types,
        string sort,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Retrieving transactions for CPF: {Cpf}. Page: {Page}, PageSize: {PageSize}, StartDate: {StartDate}, EndDate: {EndDate}, Types: {Types}",
            cpf, page, pageSize, startDate, endDate, types);

        try
        {
            var natureCodes = ParseCommaSeparatedList(types);

            var queryOptions = new TransactionQueryOptions
            {
                Cpf = cpf,
                Page = page,
                PageSize = pageSize,
                StartDate = startDate,
                EndDate = endDate,
                NatureCodes = natureCodes,
                SortDirection = sort
            };

            var result = await transactionService.GetTransactionsByCpfAsync(queryOptions, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("Failed to retrieve transactions for CPF: {Cpf}. Error: {Error}", cpf, result.ErrorMessage);
                return Result<PagedResult<Transaction>>.Failure(result.ErrorMessage ?? "Failed to retrieve transactions");
            }

            logger.LogInformation(
                "Successfully retrieved transactions for CPF: {Cpf}. Count: {Count}, TotalCount: {TotalCount}",
                cpf, result.Data?.Items.Count, result.Data?.TotalCount);

            return Result<PagedResult<Transaction>>.Success(result.Data!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error retrieving transactions for CPF: {Cpf}", cpf);
            return Result<PagedResult<Transaction>>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }

    public async Task<Result<decimal>> GetBalanceByCpfAsync(string cpf, CancellationToken cancellationToken)
    {
        logger.LogInformation("Calculating balance for CPF: {Cpf}", cpf);

        try
        {
            var result = await transactionService.GetBalanceByCpfAsync(cpf, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning("Failed to calculate balance for CPF: {Cpf}. Error: {Error}", cpf, result.ErrorMessage);
                return Result<decimal>.Failure(result.ErrorMessage ?? "Failed to calculate balance");
            }

            logger.LogInformation("Successfully calculated balance for CPF: {Cpf}. Balance: {Balance}", cpf, result.Data);

            return Result<decimal>.Success(result.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error calculating balance for CPF: {Cpf}", cpf);
            return Result<decimal>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }

    public async Task<Result<PagedResult<Transaction>>> SearchTransactionsByDescriptionAsync(
        string cpf,
        string searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Searching transactions for CPF: {Cpf}. SearchTerm: {SearchTerm}, Page: {Page}, PageSize: {PageSize}",
            cpf, searchTerm, page, pageSize);

        try
        {
            var result = await transactionService.SearchTransactionsByDescriptionAsync(
                cpf, searchTerm, page, pageSize, cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogWarning(
                    "Search failed for CPF: {Cpf}, SearchTerm: {SearchTerm}. Error: {Error}",
                    cpf, searchTerm, result.ErrorMessage);
                return Result<PagedResult<Transaction>>.Failure(result.ErrorMessage ?? "Search failed");
            }

            logger.LogInformation(
                "Search completed for CPF: {Cpf}, SearchTerm: {SearchTerm}. Results: {Count}",
                cpf, searchTerm, result.Data?.Items.Count);

            return Result<PagedResult<Transaction>>.Success(result.Data!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error searching transactions for CPF: {Cpf}, SearchTerm: {SearchTerm}", cpf, searchTerm);
            return Result<PagedResult<Transaction>>.Failure($"An unexpected error occurred: {ex.Message}");
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
    /// Determines the appropriate HTTP status code based on error message.
    /// </summary>
    private static UploadStatusCode DetermineUploadStatusCode(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return UploadStatusCode.BadRequest;

        if (errorMessage.Contains("empty", StringComparison.OrdinalIgnoreCase))
            return UploadStatusCode.BadRequest;

        if (errorMessage.Contains("not allowed", StringComparison.OrdinalIgnoreCase))
            return UploadStatusCode.UnsupportedMediaType;

        if (errorMessage.Contains("exceeds maximum size", StringComparison.OrdinalIgnoreCase))
            return UploadStatusCode.PayloadTooLarge;

        if (errorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            return UploadStatusCode.UnprocessableEntity;

        return UploadStatusCode.BadRequest;
    }

    /// <summary>
    /// Stores the CNAB file content in object storage for audit trail.
    /// Failures are logged but do not affect the upload operation.
    /// </summary>
    /// <param name="fileContent">The file content to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task StoreFileInObjectStorageAsync(string fileContent, CancellationToken cancellationToken)
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store CNAB file in object storage. Continuing with upload.");
            // Don't fail the upload if storage fails - the transaction data is already saved
        }
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
