using CnabApi.Common;
using CnabApi.Models;
using CnabApi.Services.Interfaces;
using CnabApi.Services.Resilience;
using CnabApi.Utilities;
using System.Security.Cryptography;
using System.Text;

namespace CnabApi.Services;

/// <summary>
/// Implementation of CNAB upload service.
/// Orchestrates the workflow: File Reading → Parsing → Database Storage.
/// Includes resilience patterns: retry with exponential backoff, idempotency keys.
/// </summary>
/// <remarks>
/// Initializes the CNAB upload service with required dependencies.
/// </remarks>
public class CnabUploadService(
    ICnabParserService parserService,
    ITransactionService transactionService,
    IFileUploadTrackingService fileUploadTrackingService,
    ILogger<CnabUploadService> logger) : ICnabUploadService
{
    private readonly ICnabParserService _parserService = parserService;
    private readonly ITransactionService _transactionService = transactionService;
    private readonly IFileUploadTrackingService _fileUploadTrackingService = fileUploadTrackingService;
    private readonly ILogger<CnabUploadService> _logger = logger;

    /// <summary>
    /// Processes a complete CNAB file upload workflow with resilience and line-level duplicate detection.
    /// Accepts file content as a string for streaming/memory-efficient processing.
    /// </summary>
    /// <param name="fileContent">The CNAB file content to process</param>
    /// <param name="fileUploadId">The FileUpload ID for tracking line hashes (optional for backward compatibility)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with the count of successfully imported transactions.</returns>
    public async Task<Result<int>> ProcessCnabUploadAsync(string fileContent, Guid fileUploadId = default, CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Validate file content
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                _logger.LogWarning("File content validation failed: {Error}", "File was not provided or is empty.");
                return Result<int>.Failure("File was not provided or is empty.");
            }

            // Step 2: Parse file content
            var parseResult = _parserService.ParseCnabFile(fileContent);
            if (!parseResult.IsSuccess)
            {
                _logger.LogWarning("File parsing failed: {Error}", parseResult.ErrorMessage);
                return Result<int>.Failure(parseResult.ErrorMessage ?? "File parsing failed");
            }

            // Step 3: Compute file hash for idempotency
            var fileHash = ComputeFileHash(fileContent);
            
            // Step 4: Validate lines for duplicates and build transactions with idempotency
            var (transactionsWithIdempotency, duplicateLineCount) = await ValidateAndBuildTransactionsWithIdempotencyAsync(
                parseResult.Data!,
                fileContent,
                fileHash,
                fileUploadId,
                cancellationToken);

            // Step 4.5: Commit all pending line hashes to database (if any were recorded)
            if (fileUploadId != default)
            {
                try
                {
                    await _fileUploadTrackingService.CommitLineHashesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to commit line hashes to database");
                    return Result<int>.Failure("An error occurred while recording duplicate detection data. Please try again.");
                }
            }

            // Step 5: Add transactions to database with retry policy
            var retryPolicy = ResiliencePolicies.GetDatabaseRetryPolicy(_logger);
            var context = new Polly.Context { ["correlationId"] = CorrelationIdHelper.GetCorrelationId() };

            Result<List<Transaction>>? addResult = null;
            
            // If all lines were duplicates, there are no transactions to add - that's OK
            if (transactionsWithIdempotency.Count == 0)
            {
                _logger.LogInformation(
                    "All lines were duplicates. No new transactions to import. Total duplicates skipped: {DuplicateLineCount}",
                    duplicateLineCount);
                
                return Result<int>.Success(transactionsWithIdempotency.Count);
            }
            
            try
            {
                await retryPolicy.ExecuteAsync(async (ctx) =>
                {
                    addResult = await _transactionService.AddTransactionsAsync(transactionsWithIdempotency, cancellationToken);
                    
                    if (addResult != null && !addResult.IsSuccess)
                    {
                        throw new InvalidOperationException(addResult.ErrorMessage);
                    }
                }, context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError("Database transaction storage failed after retries: {Error}", ex.Message);
                return Result<int>.Failure(addResult?.ErrorMessage ?? "Database transaction storage failed after retries");
            }

            if (addResult == null || !addResult.IsSuccess)
            {
                _logger.LogError("Database transaction storage failed: {Error}", addResult?.ErrorMessage);
                return Result<int>.Failure(addResult?.ErrorMessage ?? "Database transaction storage failed");
            }

            var transactionCount = addResult.Data!.Count;
            _logger.LogInformation(
                "Successfully imported {Count} transactions (skipped {SkippedCount} duplicates) with idempotency",
                transactionCount,
                duplicateLineCount);
            
            return Result<int>.Success(transactionCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CNAB file upload processing");
            return Result<int>.Failure("An unexpected error occurred while processing the file.");
        }
    }

    /// <summary>
    /// Validates transactions for duplicate lines and builds them with idempotency keys.
    /// Skips duplicate lines if fileUploadId is provided, otherwise processes all lines.
    /// </summary>
    /// <param name="transactions">Parsed transactions from the CNAB file</param>
    /// <param name="fileContent">Original file content for line extraction</param>
    /// <param name="fileHash">Computed file hash for idempotency key generation</param>
    /// <param name="fileUploadId">FileUpload ID for line duplicate detection (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing processed transactions and count of duplicate lines skipped</returns>
    private async Task<(List<Transaction>, int)> ValidateAndBuildTransactionsWithIdempotencyAsync(
        List<Transaction> transactions,
        string fileContent,
        string fileHash,
        Guid fileUploadId,
        CancellationToken cancellationToken)
    {
        var lines = fileContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var transactionsWithIdempotency = new List<Transaction>();
        var duplicateLineCount = 0;

        for (int index = 0; index < transactions.Count; index++)
        {
            var transaction = transactions[index];
            var lineContent = index < lines.Length ? lines[index] : string.Empty;

            // Check for duplicate lines if fileUploadId is provided
            if (fileUploadId != default)
            {
                var lineHash = await _fileUploadTrackingService.CalculateFileHashAsync(
                    new MemoryStream(Encoding.UTF8.GetBytes(lineContent)));
                
                var isLineUnique = await _fileUploadTrackingService.IsLineUniqueAsync(lineHash, cancellationToken);
                
                if (!isLineUnique)
                {
                    _logger.LogWarning(
                        "Duplicate line detected and skipped. FileUploadId: {FileUploadId}, LineIndex: {LineIndex}",
                        fileUploadId,
                        index);
                    duplicateLineCount++;
                    continue;
                }

                // Record the line hash for future duplicate detection
                await _fileUploadTrackingService.RecordLineHashAsync(
                    fileUploadId,
                    lineHash,
                    lineContent,
                    cancellationToken);
            }

            // Add idempotency key to transaction
            transaction.IdempotencyKey = GenerateIdempotencyKey(fileHash, index);
            transactionsWithIdempotency.Add(transaction);
        }

        // Log skipped duplicate lines
        if (duplicateLineCount > 0)
        {
            _logger.LogInformation(
                "Skipped {DuplicateLineCount} duplicate lines during processing. FileUploadId: {FileUploadId}",
                duplicateLineCount,
                fileUploadId);
        }

        return (transactionsWithIdempotency, duplicateLineCount);
    }

    /// <summary>
    /// Generates a unique idempotency key for a transaction line.
    /// Prevents duplicate processing in case of retries.
    /// </summary>
    private static string GenerateIdempotencyKey(string fileHash, int lineIndex)
    {
        return $"{fileHash}:{lineIndex}";
    }

    private static string ComputeFileHash(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }
}
