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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    /// Retrieves all transactions for a specific CPF ordered by date (most recent first).
    /// </summary>
    /// <param name="cpf">The CPF to filter transactions.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>List of transactions for the specified CPF.</returns>
    [HttpGet("{cpf}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<Transaction>>> GetTransactionsByCpf(string cpf, CancellationToken cancellationToken)
    {
        var result = await _transactionService.GetTransactionsByCpfAsync(cpf, cancellationToken);
        
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> GetBalance(string cpf, CancellationToken cancellationToken)
    {
        var result = await _transactionService.GetBalanceByCpfAsync(cpf, cancellationToken);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { balance = result.Data });
    }

    /// <summary>
    /// Clears all transactions and stores from the database.
    /// WARNING: This operation cannot be undone.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Success message.</returns>
    [HttpDelete]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearData(CancellationToken cancellationToken)
    {
        var result = await _transactionService.ClearAllDataAsync(cancellationToken);
        
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "All data cleared successfully" });
    }
}
