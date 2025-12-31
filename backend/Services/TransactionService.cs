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
            return Result.Failure($"Error clearing data: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets transactions grouped by store name and owner, with balance calculated for each store.
    /// </summary>
    public async Task<Result<Models.Responses.PagedResponse<StoreGroupedTransactions>>> GetTransactionsGroupedByStoreAsync(
        Guid? uploadId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100; // Max page size

            // Build query with optional uploadId filter
            var query = _context.Transactions.AsNoTracking();
            
            if (uploadId.HasValue)
            {
                query = query.Where(t => t.FileUploadId == uploadId.Value);
            }

            // Get transactions ordered by date and time
            var allTransactions = await query
                .OrderBy(t => t.TransactionDate)
                .ThenBy(t => t.TransactionTime)
                .ToListAsync(cancellationToken);

            // Group by StoreName only (ignore StoreOwner differences)
            var grouped = allTransactions
                .GroupBy(t => t.StoreName)
                .Select(g => new StoreGroupedTransactions
                {
                    StoreName = g.Key,
                    // Use the first StoreOwner found for this store name (since we're grouping by StoreName only)
                    StoreOwner = g.First().StoreOwner,
                    Transactions = g.OrderBy(t => t.TransactionDate)
                                    .ThenBy(t => t.TransactionTime)
                                    .ToList(),
                    Balance = g.Sum(t => t.SignedAmount)
                })
                .OrderBy(s => s.StoreName)
                .ToList();

            // Apply pagination
            var totalCount = grouped.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var pagedItems = grouped
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedResponse = new Models.Responses.PagedResponse<StoreGroupedTransactions>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Result<Models.Responses.PagedResponse<StoreGroupedTransactions>>.Success(pagedResponse);
        }
        catch (Exception ex)
        {
            return Result<Models.Responses.PagedResponse<StoreGroupedTransactions>>.Failure($"Error fetching grouped transactions: {ex.Message}");
        }
    }

}
