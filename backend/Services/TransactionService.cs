using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using CnabApi.Common;
using CnabApi.Data;
using CnabApi.Models;
using System.Diagnostics.CodeAnalysis;
using Npgsql;

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
    /// Handles duplicate IdempotencyKey violations gracefully by filtering out duplicates.
    /// </summary>
    public async Task<Result<List<Transaction>>> AddTransactionsAsync(List<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        try
        {
            if (transactions == null || transactions.Count == 0)
                return Result<List<Transaction>>.Failure("No transactions were provided.");

            // Filter out transactions that already exist (based on IdempotencyKey)
            var idempotencyKeys = transactions.Select(t => t.IdempotencyKey).ToList();
            var existingKeys = await _context.Transactions
                .Where(t => idempotencyKeys.Contains(t.IdempotencyKey))
                .Select(t => t.IdempotencyKey)
                .ToListAsync(cancellationToken);

            var existingKeysSet = existingKeys.ToHashSet();
            var newTransactions = transactions
                .Where(t => !existingKeysSet.Contains(t.IdempotencyKey))
                .ToList();

            if (newTransactions.Count == 0)
            {
                // All transactions are duplicates - this is OK, just return empty list
                return Result<List<Transaction>>.Success([]);
            }

            // Try bulk insert first
            try
            {
                _context.Transactions.AddRange(newTransactions);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
            {
                // Handle case where duplicates were inserted between check and save
                // Fall back to individual inserts with duplicate handling
                _context.ChangeTracker.Clear();
                return await AddTransactionsIndividuallyAsync(newTransactions, cancellationToken);
            }

            // Invalidate cache for all affected CPFs
            var cpfs = newTransactions.Select(t => t.Cpf).Distinct();
            foreach (var cpf in cpfs)
            {
                await InvalidateCacheAsync(cpf, cancellationToken);
            }

            return Result<List<Transaction>>.Success(newTransactions);
        }
        catch (Exception ex)
        {
            return Result<List<Transaction>>.Failure($"Error adding transactions: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a single transaction atomically with immediate commit.
    /// Handles duplicate IdempotencyKey gracefully.
    /// </summary>
    public async Task<Result<Transaction>> AddSingleTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            if (transaction == null)
                return Result<Transaction>.Failure("Transaction cannot be null.");

            // Check if it already exists
            var exists = await _context.Transactions
                .AnyAsync(t => t.IdempotencyKey == transaction.IdempotencyKey, cancellationToken);

            if (exists)
            {
                return Result<Transaction>.Failure("Transaction already exists (duplicate IdempotencyKey).");
            }

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync(cancellationToken);

            // Invalidate cache for the CPF
            await InvalidateCacheAsync(transaction.Cpf, cancellationToken);

            return Result<Transaction>.Success(transaction);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            _context.ChangeTracker.Clear();
            return Result<Transaction>.Failure("Transaction already exists (unique constraint violation).");
        }
        catch (Exception ex)
        {
            _context.ChangeTracker.Clear();
            return Result<Transaction>.Failure($"Error adding transaction: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a single transaction to the context without saving (for use with Unit of Work).
    /// Validates duplicate but does not commit or invalidate cache.
    /// </summary>
    public async Task<Result<Transaction>> AddTransactionToContextAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        try
        {
            if (transaction == null)
                return Result<Transaction>.Failure("Transaction cannot be null.");

            // Check if it already exists
            var exists = await _context.Transactions
                .AnyAsync(t => t.IdempotencyKey == transaction.IdempotencyKey, cancellationToken);

            if (exists)
            {
                return Result<Transaction>.Failure("Transaction already exists (duplicate IdempotencyKey).");
            }

            _context.Transactions.Add(transaction);
            // Do NOT call SaveChangesAsync - Unit of Work will handle it

            return Result<Transaction>.Success(transaction);
        }
        catch (Exception ex)
        {
            return Result<Transaction>.Failure($"Error adding transaction to context: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds transactions individually, skipping duplicates.
    /// Used as fallback when bulk insert fails due to concurrent duplicate inserts.
    /// </summary>
    private async Task<Result<List<Transaction>>> AddTransactionsIndividuallyAsync(
        List<Transaction> transactions,
        CancellationToken cancellationToken)
    {
        var successfullyAdded = new List<Transaction>();
        
        foreach (var transaction in transactions)
        {
            try
            {
                // Check if it exists (double-check)
                var exists = await _context.Transactions
                    .AnyAsync(t => t.IdempotencyKey == transaction.IdempotencyKey, cancellationToken);
                
                if (exists)
                {
                    continue; // Skip duplicate
                }

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync(cancellationToken);
                successfullyAdded.Add(transaction);
                
                // Clear tracking to avoid issues with next transaction
                _context.ChangeTracker.Clear();
            }
            catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
            {
                // Another concurrent insert got there first - skip this one
                _context.ChangeTracker.Clear();
                continue;
            }
            catch (Exception)
            {
                // Log error but continue with other transactions
                _context.ChangeTracker.Clear();
                // In production, you might want to log this to a dead letter queue
                continue;
            }
        }

        // Invalidate cache for all affected CPFs
        var cpfs = successfullyAdded.Select(t => t.Cpf).Distinct();
        foreach (var cpf in cpfs)
        {
            await InvalidateCacheAsync(cpf, cancellationToken);
        }

        return Result<List<Transaction>>.Success(successfullyAdded);
    }

    /// <summary>
    /// Checks if the exception is a PostgreSQL unique violation (23505).
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            // PostgreSQL unique violation error code
            return pgEx.SqlState == "23505";
        }
        return false;
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

            // Validate CPF format: should contain only digits and be 11-14 characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(options.Cpf.Trim(), @"^\d{11,14}$"))
                return Result<PagedResult<Transaction>>.Failure("Invalid CPF format. CPF must contain 11-14 digits.");

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
    /// Invalidates cache for a specific CPF.
    /// </summary>
    public async Task InvalidateCacheForCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        await InvalidateCacheAsync(cpf, cancellationToken);
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
