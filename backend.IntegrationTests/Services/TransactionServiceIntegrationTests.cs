using CnabApi.IntegrationTests.Infrastructure;
using CnabApi.Models;
using CnabApi.Services;
using CnabApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;
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
                TransactionTime = TimeSpan.FromHours(12)
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
                TransactionTime = TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(1))
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

    [Fact]
    public async Task AddTransactionsAsync_InvalidatesCache_ForAffectedCpfs()
    {
        // Arrange
        using var context = DbContext;
        var cacheMock = new Mock<IDistributedCache>();
        var transactionService = new TransactionService(context, cacheMock.Object);
        
        var transactions = new List<Transaction>
        {
            new()
            {
                BankCode = "0001",
                Cpf = "12345678901",
                NatureCode = "4",
                Amount = 100m,
                Card = "123456",
                StoreOwner = "Owner",
                StoreName = "Store",
                TransactionDate = DateTime.UtcNow,
                TransactionTime = TimeSpan.Zero
            }
        };

        // Act
        var result = await transactionService.AddTransactionsAsync(transactions);

        // Assert
        Assert.True(result.IsSuccess);
        // Cache remove was called for the CPF
        cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithExistingCpf_ReturnsTransactions()
    {
        // Arrange
        using var context = DbContext;
        var cacheStub = new Mock<IDistributedCache>();
        var transactionService = new TransactionService(context, cacheStub.Object);
        
        var cpf = "12345678901";
        var transactions = new List<Transaction>
        {
            new()
            {
                BankCode = "0001",
                Cpf = cpf,
                NatureCode = "4",
                Amount = 100m,
                Card = "123456",
                StoreOwner = "Owner",
                StoreName = "Store",
                TransactionDate = DateTime.UtcNow,
                TransactionTime = TimeSpan.Zero
            },
            new()
            {
                BankCode = "0001",
                Cpf = cpf,
                NatureCode = "1",
                Amount = 50m,
                Card = "123456",
                StoreOwner = "Owner",
                StoreName = "Store",
                TransactionDate = DateTime.UtcNow.AddDays(-1),
                TransactionTime = TimeSpan.Zero
            }
        };

        await transactionService.AddTransactionsAsync(transactions);

        // Act
        var options = new TransactionQueryOptions { Cpf = cpf };
        var result = await transactionService.GetTransactionsByCpfAsync(options);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data?.Items?.Count);
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithInvalidCpf_ReturnsFailure()
    {
        // Arrange
        using var context = DbContext;
        var cacheStub = new Mock<IDistributedCache>();
        var transactionService = new TransactionService(context, cacheStub.Object);

        // Act
        var options = new TransactionQueryOptions { Cpf = "invalid" };
        var result = await transactionService.GetTransactionsByCpfAsync(options);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        using var context = DbContext;
        var cacheStub = new Mock<IDistributedCache>();
        var transactionService = new TransactionService(context, cacheStub.Object);
        
        var cpf = "12345678901";
        
        // Add 5 transactions
        var transactions = Enumerable.Range(0, 5).Select(i => new Transaction
        {
            BankCode = "0001",
            Cpf = cpf,
            NatureCode = "4",
            Amount = 100m + i,
            Card = "123456",
            StoreOwner = "Owner",
            StoreName = "Store",
            TransactionDate = DateTime.UtcNow.AddDays(-i),
            TransactionTime = TimeSpan.FromHours(i)
        }).ToList();

        await transactionService.AddTransactionsAsync(transactions);

        // Act
        var options = new TransactionQueryOptions { Cpf = cpf, PageSize = 2 };
        var result = await transactionService.GetTransactionsByCpfAsync(options);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True((result.Data?.Items?.Count ?? 0) <= 2);
    }
}
