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
    /// Retrieves transactions for a specific CPF ordered by date descending.
    /// </summary>
    public async Task<Result<List<Transaction>>> GetTransactionsByCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return Result<List<Transaction>>.Failure("CPF é obrigatório.");

            var transactions = await _context.Transactions
                .Where(t => t.Cpf == cpf)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync(cancellationToken);

            return Result<List<Transaction>>.Success(transactions);
        }
        catch (Exception ex)
        {
            return Result<List<Transaction>>.Failure($"Erro ao obter transações: {ex.Message}");
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
