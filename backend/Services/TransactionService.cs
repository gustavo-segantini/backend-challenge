using Microsoft.EntityFrameworkCore;
using CnabApi.Common;
using CnabApi.Data;
using CnabApi.Models;
using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Services;

/// <summary>
/// Implementation of transaction service.
/// </summary>
[ExcludeFromCodeCoverage]
public class TransactionService(CnabDbContext context) : ITransactionService
{
    private readonly CnabDbContext _context = context;

    /// <summary>
    /// Adds transactions to the database and updates store balances.
    /// </summary>
public async Task<Result<List<Transaction>>> AddTransactionsAsync(List<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        try
        {
            if (transactions == null || transactions.Count == 0)
                return Result<List<Transaction>>.Failure("Nenhuma transação foi fornecida.");

            _context.Transactions.AddRange(transactions);
            
            await _context.SaveChangesAsync(cancellationToken);

            return Result<List<Transaction>>.Success(transactions);
        }
        catch (Exception ex)
        {
            return Result<List<Transaction>>.Failure($"Erro ao adicionar transações: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves transactions for a specific CPF ordered by date descending, with pagination and optional filters.
    /// </summary>
    public async Task<Result<PagedResult<Transaction>>> GetTransactionsByCpfAsync(TransactionQueryOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.Cpf))
                return Result<PagedResult<Transaction>>.Failure("CPF é obrigatório.");

            if (options.Page <= 0 || options.PageSize <= 0)
                return Result<PagedResult<Transaction>>.Failure("Parâmetros de paginação inválidos.");

            var query = _context.Transactions
                .AsNoTracking()
                .Where(t => t.Cpf == options.Cpf);

            if (options.StartDate.HasValue)
            {
                var start = options.StartDate.Value.Date;
                query = query.Where(t => t.TransactionDate.Date >= start);
            }

            if (options.EndDate.HasValue)
            {
                var end = options.EndDate.Value.Date;
                query = query.Where(t => t.TransactionDate.Date <= end);
            }

            if (options.NatureCodes is { Count: > 0 })
            {
                query = query.Where(t => options.NatureCodes!.Contains(t.NatureCode));
            }

            query = options.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderBy(t => t.TransactionDate).ThenBy(t => t.TransactionTime)
                : query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.TransactionTime);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((options.Page - 1) * options.PageSize)
                .Take(options.PageSize)
                .ToListAsync(cancellationToken);

            var paged = new PagedResult<Transaction>
            {
                Items = items,
                TotalCount = totalCount,
                Page = options.Page,
                PageSize = options.PageSize
            };

            return Result<PagedResult<Transaction>>.Success(paged);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<Transaction>>.Failure($"Erro ao obter transações: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates the total balance for a specific CPF.
    /// </summary>
    public async Task<Result<decimal>> GetBalanceByCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return Result<decimal>.Failure("CPF é obrigatório.");

            var transactions = await _context.Transactions
                .Where(t => t.Cpf == cpf)
                .ToListAsync(cancellationToken);

            var totalBalance = transactions.Sum(t => t.SignedAmount);

            return Result<decimal>.Success(totalBalance);
        }
        catch (Exception ex)
        {
            return Result<decimal>.Failure($"Erro ao calcular balanço: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all transactions from the database.
    /// </summary>
    public async Task<Result> ClearAllDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Transactions.RemoveRange(_context.Transactions);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Erro ao limpar dados: {ex.Message}");
        }
    }
}
