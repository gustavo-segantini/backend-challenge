using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CnabApi.Models;
using CnabApi.Services;

namespace CnabApi.Controllers;

/// <summary>
/// API controller for managing CNAB file uploads and transaction queries.
/// 
/// This controller handles:
/// - CNAB file uploads and parsing
/// - Transaction queries with advanced filtering and pagination
/// - Balance calculations by CPF
/// - Transaction search functionality
/// - Admin operations for data management
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
[Tags("Transactions")]
public class TransactionsController(
    ICnabUploadService uploadService,
    ITransactionService transactionService,
    ILogger<TransactionsController> logger) : ControllerBase
{
    private readonly ICnabUploadService _uploadService = uploadService;
    private readonly ITransactionService _transactionService = transactionService;
    private readonly ILogger<TransactionsController> _logger = logger;

    /// <summary>
    /// Uploads a CNAB file, parses transactions, and stores them in the database.
    /// 
    /// Expects a CNAB-formatted text file with 240-character lines per transaction.
    /// Each line is parsed and validated against CNAB specifications.
    /// </summary>
    /// <param name="file">The CNAB file to upload (format: .txt, max 10MB).</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Success message with transaction count or error details.</returns>
    /// <remarks>
    /// **Sample Request:**
    /// ```
    /// POST /api/v1/transactions/upload
    /// Authorization: Bearer {token}
    /// Content-Type: multipart/form-data
    /// 
    /// file: [CNAB format text file]
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
    /// - 413: File too large (exceeds 10MB)
    /// </remarks>
    [HttpPost("upload")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadCnabFile(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting CNAB file upload process. File: {FileName}, Size: {FileSize} bytes", 
                file?.FileName, file?.Length);

            if (file == null)
            {
                _logger.LogWarning("File reading failed: {Error}", "Arquivo não foi fornecido ou está vazio.");
                return BadRequest(new { error = "Arquivo não foi fornecido ou está vazio." });
            }

            var result = await _uploadService.ProcessCnabUploadAsync(file, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("CNAB file upload failed. Error: {Error}", result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("CNAB file upload completed successfully. Transactions imported: {Count}", result.Data);

            return Ok(new
            {
                message = $"Successfully imported {result.Data} transactions",
                count = result.Data
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CNAB file upload");
            throw;
        }
    }

    /// <summary>
    /// Retrieves transactions for a CPF with pagination, filtering, and ordering.
    /// 
    /// Supports advanced query options to retrieve transactions for specific CPF/CNPJ
    /// with optional date range filtering, transaction type filtering, and sorting.
    /// </summary>
    /// <param name="cpf">The CPF/CNPJ to filter transactions (11-14 digits).</param>
    /// <param name="page">Page number (starting at 1). Default: 1.</param>
    /// <param name="pageSize">Items per page (1-100). Default: 50.</param>
    /// <param name="startDate">Filter by start date inclusive (format: YYYY-MM-DD or ISO 8601).</param>
    /// <param name="endDate">Filter by end date inclusive (format: YYYY-MM-DD or ISO 8601).</param>
    /// <param name="types">Comma-separated nature codes to filter (e.g., 1,2,3).</param>
    /// <param name="sort">Sort direction by date: 'asc' or 'desc' (default: 'desc' - most recent first).</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Paged list of transactions matching the criteria.</returns>
    /// <remarks>
    /// Nature Codes:
    /// - 1: Débito (debit/expense)
    /// - 2: Crédito (credit/income)
    /// - 3: Transferência (transfer)
    /// - 4: Aluguel (rent)
    /// - 5: Salário (salary)
    /// 
    /// Sample Request:
    /// GET /api/v1/transactions/12345678901?page=1&amp;pageSize=20&amp;startDate=2019-01-01&amp;endDate=2019-12-31&amp;types=1,2&amp;sort=desc
    /// Authorization: Bearer {token}
    /// 
    /// Sample Response (200):
    /// {
    ///   "items": [
    ///     {
    ///       "id": "123e4567-e89b-12d3-a456-426614174000",
    ///       "cpf": "12345678901",
    ///       "storeName": "Restaurante XYZ",
    ///       "storeOwner": "João Silva",
    ///       "transactionDate": "2019-06-15",
    ///       "transactionTime": "14:30:00",
    ///       "transactionValue": 150.50,
    ///       "natureCode": 2,
    ///       "createdAt": "2025-12-21T10:30:00Z"
    ///     }
    ///   ],
    ///   "totalCount": 45,
    ///   "pageSize": 20,
    ///   "currentPage": 1
    /// }
    /// 
    /// Error Cases:
    /// - 400: Invalid CPF format, invalid date range, or query parameters
    /// - 401: Missing or invalid authentication token
    /// - 404: No transactions found for the provided CPF
    /// </remarks>
    [HttpGet("{cpf}")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<Transaction>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<Transaction>>> GetTransactionsByCpf(
        string cpf,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? types = null,
        [FromQuery] string sort = "desc",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving transactions for CPF: {Cpf}. Page: {Page}, PageSize: {PageSize}, StartDate: {StartDate}, EndDate: {EndDate}, Types: {Types}",
            cpf, page, pageSize, startDate, endDate, types);

        try
        {
            var natureCodes = string.IsNullOrWhiteSpace(types)
                ? new List<string>()
                : types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

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

            var result = await _transactionService.GetTransactionsByCpfAsync(queryOptions, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to retrieve transactions for CPF: {Cpf}. Error: {Error}", cpf, result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("Successfully retrieved transactions for CPF: {Cpf}. Count: {Count}, TotalCount: {TotalCount}",
                cpf, result.Data?.Items.Count, result.Data?.TotalCount);

            return Ok(result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving transactions for CPF: {Cpf}", cpf);
            throw;
        }
    }

    /// <summary>
    /// Calculates and returns the total balance for a specific CPF.
    /// 
    /// Balance is calculated as sum of all transactions:
    /// - Positive values (nature codes 2,3,5): Income/credits
    /// - Negative values (nature codes 1,4): Expenses/debits
    /// </summary>
    /// <param name="cpf">The CPF/CNPJ to calculate balance for (11-14 digits).</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Object containing the total balance value for that CPF.</returns>
    /// <remarks>
    /// **Sample Request:**
    /// ```
    /// GET /api/v1/transactions/12345678901/balance
    /// Authorization: Bearer {token}
    /// ```
    /// 
    /// **Sample Response (200):**
    /// ```json
    /// {
    ///   "balance": 5250.75
    /// }
    /// ```
    /// 
    /// **Error Cases:**
    /// - 400: Invalid CPF format
    /// - 401: Missing or invalid authentication token
    /// - 404: No transactions found for the provided CPF
    /// </remarks>
    [HttpGet("{cpf}/balance")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetBalance(string cpf, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calculating balance for CPF: {Cpf}", cpf);

        try
        {
            var result = await _transactionService.GetBalanceByCpfAsync(cpf, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to calculate balance for CPF: {Cpf}. Error: {Error}", cpf, result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("Successfully calculated balance for CPF: {Cpf}. Balance: {Balance}", cpf, result.Data);

            return Ok(new { balance = result.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calculating balance for CPF: {Cpf}", cpf);
            throw;
        }
    }

    /// <summary>
    /// Searches transactions by description for a specific CPF using full-text search.
    /// 
    /// Searches across store names and owner names for case-insensitive matches.
    /// </summary>
    /// <param name="cpf">The CPF to filter transactions (11-14 digits).</param>
    /// <param name="searchTerm">The search term to find in transaction descriptions (min 2 characters).</param>
    /// <param name="page">Page number (starting at 1). Default: 1.</param>
    /// <param name="pageSize">Items per page (1-100). Default: 50.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Paged list of matching transactions.</returns>
    /// <remarks>
    /// Sample Request:
    /// GET /api/v1/transactions/12345678901/search?searchTerm=restaurante&amp;page=1&amp;pageSize=20
    /// Authorization: Bearer {token}
    /// 
    /// Sample Response (200):
    /// {
    ///   "items": [
    ///     {
    ///       "id": "123e4567-e89b-12d3-a456-426614174000",
    ///       "cpf": "12345678901",
    ///       "storeName": "Restaurante XYZ",
    ///       "storeOwner": "João Silva",
    ///       "transactionDate": "2019-06-15",
    ///       "transactionTime": "14:30:00",
    ///       "transactionValue": 150.50,
    ///       "natureCode": 2
    ///     }
    ///   ],
    ///   "totalCount": 5,
    ///   "pageSize": 20,
    ///   "currentPage": 1
    /// }
    /// 
    /// Error Cases:
    /// - 400: Invalid CPF format or search term too short
    /// - 401: Missing or invalid authentication token
    /// - 404: No results found
    /// </remarks>
    [HttpGet("{cpf}/search")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<Transaction>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<Transaction>>> SearchTransactions(
        string cpf,
        [FromQuery] string searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching transactions for CPF: {Cpf}. SearchTerm: {SearchTerm}, Page: {Page}, PageSize: {PageSize}",
            cpf, searchTerm, page, pageSize);

        try
        {
            var result = await _transactionService.SearchTransactionsByDescriptionAsync(
                cpf,
                searchTerm,
                page,
                pageSize,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Search failed for CPF: {Cpf}, SearchTerm: {SearchTerm}. Error: {Error}", cpf, searchTerm, result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("Search completed for CPF: {Cpf}, SearchTerm: {SearchTerm}. Results: {Count}",
                cpf, searchTerm, result.Data?.Items.Count);

            return Ok(result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error searching transactions for CPF: {Cpf}, SearchTerm: {SearchTerm}", cpf, searchTerm);
            throw;
        }
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
        _logger.LogWarning("Admin initiated data clear operation");

        try
        {
            var result = await _transactionService.ClearAllDataAsync(cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to clear data. Error: {Error}", result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation("Data cleared successfully");

            return Ok(new { message = "All data cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during data clear operation");
            throw;
        }
    }
}
