using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CnabApi.Models;
using CnabApi.Services;

namespace CnabApi.Controllers;

/// <summary>
/// API controller for managing CNAB file uploads and transaction queries.
/// </summary>
[ApiController]
[Route("api/[controller]")]
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
    /// </summary>
    /// <param name="file">The CNAB file to upload (format: .txt).</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Success message with transaction count or error details.</returns>
    [HttpPost("upload")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadCnabFile(IFormFile file, CancellationToken cancellationToken)
    {
        var result = await _uploadService.ProcessCnabUploadAsync(file, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new
        {
            message = $"Successfully imported {result.Data} transactions",
            count = result.Data
        });
    }

    /// <summary>
    /// Retrieves transactions for a CPF with pagination, filters and ordering.
    /// </summary>
    /// <param name="cpf">The CPF to filter transactions.</param>
    /// <param name="page">Page number (starting at 1).</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="startDate">Filter by start date (inclusive).</param>
    /// <param name="endDate">Filter by end date (inclusive).</param>
    /// <param name="types">Comma-separated nature codes to filter (e.g., 1,2,3).</param>
    /// <param name="sort">Sort direction by date/time: asc or desc (default desc).</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Paged list of transactions.</returns>
    [HttpGet("{cpf}")]
    [ProducesResponseType(typeof(PagedResult<Transaction>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
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
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Calculates and returns the total balance for a specific CPF.
    /// </summary>
    /// <param name="cpf">The CPF to calculate balance for.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Object containing the total balance value for that CPF.</returns>
    [HttpGet("{cpf}/balance")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> GetBalance(string cpf, CancellationToken cancellationToken)
    {
        var result = await _transactionService.GetBalanceByCpfAsync(cpf, cancellationToken);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { balance = result.Data });
    }

    /// <summary>
    /// Searches transactions by description for a specific CPF using full-text search.
    /// </summary>
    /// <param name="cpf">The CPF to filter transactions.</param>
    /// <param name="searchTerm">The search term to find in transaction descriptions.</param>
    /// <param name="page">Page number (starting at 1).</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Paged list of matching transactions.</returns>
    [HttpGet("{cpf}/search")]
    [ProducesResponseType(typeof(PagedResult<Transaction>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<Transaction>>> SearchTransactions(
        string cpf,
        [FromQuery] string searchTerm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _transactionService.SearchTransactionsByDescriptionAsync(
            cpf,
            searchTerm,
            page,
            pageSize,
            cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

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
        var result = await _transactionService.ClearAllDataAsync(cancellationToken);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "All data cleared successfully" });
    }
}
