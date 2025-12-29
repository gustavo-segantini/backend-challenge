using CnabApi.IntegrationTests.Infrastructure;
using CnabApi.Models;
using CnabApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;

namespace CnabApi.IntegrationTests.Services;

/// <summary>
/// Integration tests for TransactionService with real PostgreSQL database
/// </summary>
public class TransactionServiceIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task AddTransactionsAsync_WithValidTransactions_PersistsToDatabase()
    {
        // Arrange
        using var context = DbContext;
        var cacheStub = new Mock<IDistributedCache>();
        var transactionService = new TransactionService(context, cacheStub.Object);
        
        var transactions = new List<Transaction>
        {
            new()
            {
                BankCode = "0001",
                Cpf = "12345678901",
                NatureCode = "4",  // Credit/Income
                Amount = 100.50m,
                Card = "123456",
                StoreOwner = "Store Owner",
                StoreName = "Test Store",
                TransactionDate = DateTime.UtcNow,
                TransactionTime = TimeSpan.FromHours(12),
                IdempotencyKey = "test-key-1"
            },
            new()
            {
                BankCode = "0001",
                Cpf = "12345678901",
                NatureCode = "1",  // Debit/Expense
                Amount = 50.25m,
                Card = "123456",
                StoreOwner = "Store Owner",
                StoreName = "Test Store",
                TransactionDate = DateTime.UtcNow,
                TransactionTime = TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(1)),
                IdempotencyKey = "test-key-2"
            }
        };

        // Act
        var result = await transactionService.AddTransactionsAsync(transactions);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data?.Count);

        // Verify in database
        var savedTransactions = await context.Transactions
            .Where(t => t.Cpf == "12345678901")
            .OrderBy(t => t.TransactionTime)
            .ToListAsync();

        Assert.Equal(2, savedTransactions.Count);
        Assert.Equal(100.50m, savedTransactions[0].Amount);
        Assert.Equal(50.25m, savedTransactions[1].Amount);
    }

    [Fact]
    public async Task AddTransactionsAsync_WithEmptyList_ReturnsFailure()
    {
        // Arrange
        using var context = DbContext;
        var cacheStub = new Mock<IDistributedCache>();
        var transactionService = new TransactionService(context, cacheStub.Object);

        // Act
        var result = await transactionService.AddTransactionsAsync([]);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("No transactions", result.ErrorMessage ?? "");
    }

}
