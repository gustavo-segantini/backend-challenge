using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using CnabApi.Common;
using CnabApi.Data;
using CnabApi.Models;
using System.Diagnostics.CodeAnalysis;

namespace CnabApi.Services;

/// <summary>
/// Implementation of transaction service with caching and full-text search support.
/// </summary>
[ExcludeFromCodeCoverage]
public class TransactionService(CnabDbContext context, IDistributedCache cache) : ITransactionService
{
    private readonly CnabDbContext _context = context;
    private readonly IDistributedCache _cache = cache;

    private const string BalanceCacheKeyPrefix = "balance:";
    private const int CacheExpirationMinutes = 30;

    /// <summary>
    /// Adds transactions to the database and updates store balances.
    /// </summary>
    public async Task<Result<List<Transaction>>> AddTransactionsAsync(List<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        try
        {
            if (transactions == null || transactions.Count == 0)
                return Result<List<Transaction>>.Failure("No transactions were provided.");

            _context.Transactions.AddRange(transactions);
            
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache for all affected CPFs
            var cpfs = transactions.Select(t => t.Cpf).Distinct();
            foreach (var cpf in cpfs)
            {
                await InvalidateCacheAsync(cpf, cancellationToken);
            }

            return Result<List<Transaction>>.Success(transactions);
        }
        catch (Exception ex)
        {
            return Result<List<Transaction>>.Failure($"Error adding transactions: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves transactions for a specific CPF ordered by date descending, with pagination and optional filters.
    /// Uses distributed caching for performance.
    /// </summary>
    public async Task<Result<PagedResult<Transaction>>> GetTransactionsByCpfAsync(TransactionQueryOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(options.Cpf))
                return Result<PagedResult<Transaction>>.Failure("CPF is required.");

            if (options.Page <= 0 || options.PageSize <= 0)
                return Result<PagedResult<Transaction>>.Failure("Invalid pagination parameters.");

            var query = BuildTransactionQuery(options);

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
            return Result<PagedResult<Transaction>>.Failure($"Error fetching transactions: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches transactions by description using full-text search.
    /// </summary>
    public async Task<Result<PagedResult<Transaction>>> SearchTransactionsByDescriptionAsync(
        string cpf, 
        string searchTerm,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return Result<PagedResult<Transaction>>.Failure("CPF is required.");

            if (string.IsNullOrWhiteSpace(searchTerm))
                return Result<PagedResult<Transaction>>.Failure("Search term is required.");

            if (page <= 0 || pageSize <= 0)
                return Result<PagedResult<Transaction>>.Failure("Invalid pagination parameters.");

            var searchTermLower = searchTerm.ToLower();

            var query = _context.Transactions
                .AsNoTracking()
                .Where(t => t.Cpf == cpf &&
                           (t.StoreName.ToLower().Contains(searchTermLower) ||
                            t.StoreOwner.ToLower().Contains(searchTermLower) ||
                            t.NatureCode.Contains(searchTerm)))
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionTime);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var result = new PagedResult<Transaction>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Result<PagedResult<Transaction>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<Transaction>>.Failure($"Error searching transactions: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates the total balance for a specific CPF with caching.
    /// </summary>
    public async Task<Result<decimal>> GetBalanceByCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cpf))
                return Result<decimal>.Failure("CPF is required.");

            // Try to get from cache
            var cacheKey = $"{BalanceCacheKeyPrefix}{cpf}";
            var cachedBalance = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedBalance) && decimal.TryParse(cachedBalance, out var balance))
                return Result<decimal>.Success(balance);

            var transactions = await _context.Transactions
                .Where(t => t.Cpf == cpf)
                .ToListAsync(cancellationToken);

            var totalBalance = transactions.Sum(t => t.SignedAmount);

            // Cache the balance
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
            };
            await _cache.SetStringAsync(cacheKey, totalBalance.ToString(), cacheOptions, cancellationToken);

            return Result<decimal>.Success(totalBalance);
        }
        catch (Exception ex)
        {
            return Result<decimal>.Failure($"Error calculating balance: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all transactions from the database and invalidates cache.
    /// </summary>
    public async Task<Result> ClearAllDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cpfs = await _context.Transactions
                .Select(t => t.Cpf)
                .Distinct()
                .ToListAsync(cancellationToken);

            _context.Transactions.RemoveRange(_context.Transactions);
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache for all CPFs
            foreach (var cpf in cpfs)
            {
                await InvalidateCacheAsync(cpf, cancellationToken);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error clearing data: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper method to build transaction query with filters.
    /// </summary>
    private IQueryable<Transaction> BuildTransactionQuery(TransactionQueryOptions options)
    {
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

        return query;
    }

    /// <summary>
    /// Helper method to invalidate cache for a specific CPF.
    /// </summary>
    private async Task InvalidateCacheAsync(string cpf, CancellationToken cancellationToken = default)
    {
        // In a real scenario, we'd use a pattern-based cache invalidation
        // For now, we'll remove the balance cache key
        var balanceCacheKey = $"{BalanceCacheKeyPrefix}{cpf}";
        await _cache.RemoveAsync(balanceCacheKey, cancellationToken);
    }
}
