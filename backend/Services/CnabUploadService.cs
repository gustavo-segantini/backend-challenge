using CnabApi.Common;

namespace CnabApi.Services;

/// <summary>
/// Implementation of CNAB upload service.
/// Orchestrates the workflow: File Reading → Parsing → Database Storage.
/// </summary>
/// <remarks>
/// Initializes the CNAB upload service with required dependencies.
/// </remarks>
public class CnabUploadService(
    IFileService fileService,
    ICnabParserService parserService,
    ITransactionService transactionService,
    ILogger<CnabUploadService> logger) : ICnabUploadService
{
    private readonly IFileService _fileService = fileService;
    private readonly ICnabParserService _parserService = parserService;
    private readonly ITransactionService _transactionService = transactionService;
    private readonly ILogger<CnabUploadService> _logger = logger;

    /// <summary>
    /// Processes a complete CNAB file upload workflow.
    /// </summary>
    /// <returns>Result with the count of successfully imported transactions.</returns>
    public async Task<Result<int>> ProcessCnabUploadAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Read and validate file
            var readResult = await _fileService.ReadCnabFileAsync(file, cancellationToken);
            if (!readResult.IsSuccess)
            {
                _logger.LogWarning("File reading failed: {Error}", readResult.ErrorMessage);
                return Result<int>.Failure(readResult.ErrorMessage ?? "File reading failed");
            }

            // Step 2: Parse file content
            var parseResult = _parserService.ParseCnabFile(readResult.Data!);
            if (!parseResult.IsSuccess)
            {
                _logger.LogWarning("File parsing failed: {Error}", parseResult.ErrorMessage);
                return Result<int>.Failure(parseResult.ErrorMessage ?? "File parsing failed");
            }

            // Step 3: Add transactions to database
            var addResult = await _transactionService.AddTransactionsAsync(parseResult.Data!, cancellationToken);
            if (!addResult.IsSuccess)
            {
                _logger.LogError("Database transaction storage failed: {Error}", addResult.ErrorMessage);
                return Result<int>.Failure(addResult.ErrorMessage ?? "Database transaction storage failed");
            }

            var transactionCount = addResult.Data!.Count;
            _logger.LogInformation("Successfully imported {Count} transactions", transactionCount);
            
            return Result<int>.Success(transactionCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CNAB file upload processing");
            return Result<int>.Failure("An unexpected error occurred while processing the file.");
        }
    }
}
