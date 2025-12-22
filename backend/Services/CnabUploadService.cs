using CnabApi.Common;
using CnabApi.Models;
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
    ILogger<CnabUploadService> logger) : ICnabUploadService
{
    private readonly ICnabParserService _parserService = parserService;
    private readonly ITransactionService _transactionService = transactionService;
    private readonly ILogger<CnabUploadService> _logger = logger;

    /// <summary>
    /// Processes a complete CNAB file upload workflow with resilience.
    /// Accepts file content as a string for streaming/memory-efficient processing.
    /// </summary>
    /// <returns>Result with the count of successfully imported transactions.</returns>
    public async Task<Result<int>> ProcessCnabUploadAsync(string fileContent, CancellationToken cancellationToken = default)
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
            var transactionsWithIdempotency = parseResult.Data!
                .Select((transaction, index) =>
                {
                    transaction.IdempotencyKey = GenerateIdempotencyKey(fileHash, index);
                    return transaction;
                })
                .ToList();

            // Step 4: Add transactions to database with retry policy
            var retryPolicy = ResiliencePolicies.GetDatabaseRetryPolicy(_logger);
            var context = new Polly.Context { ["correlationId"] = CorrelationIdHelper.GetCorrelationId() };

            Result<List<Transaction>>? addResult = null;
            
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
            _logger.LogInformation("Successfully imported {Count} transactions with idempotency", transactionCount);
            
            return Result<int>.Success(transactionCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CNAB file upload processing");
            return Result<int>.Failure("An unexpected error occurred while processing the file.");
        }
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
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }
}
