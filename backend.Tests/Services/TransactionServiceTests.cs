using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;

namespace CnabApi.Tests.Services;

/// <summary>
/// Integration tests for the TransactionService.
/// Uses in-memory database for testing database operations.
/// </summary>
public class TransactionServiceTests : IDisposable
{
    private readonly CnabDbContext _context;
    private readonly TransactionService _transactionService;

    public TransactionServiceTests()
    {
        var options = new DbContextOptionsBuilder<CnabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CnabDbContext(options);
        var cacheMock = new Mock<IDistributedCache>();
        _transactionService = new TransactionService(_context, cacheMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region AddTransactionsAsync Tests

    [Fact]
    public async Task AddTransactionsAsync_WithValidTransactions_ShouldSaveToDatabase()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction("1", 100m, "11111111111"),
            CreateTransaction("2", 200m, "22222222222")
        };

        // Act
        var result = await _transactionService.AddTransactionsAsync(transactions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        
        var savedTransactions = await _context.Transactions.ToListAsync();
        savedTransactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddTransactionsAsync_WithEmptyList_ShouldReturnFailure()
    {
        // Arrange
        var transactions = new List<Transaction>();

        // Act
        var result = await _transactionService.AddTransactionsAsync(transactions);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No transactions were provided");
    }

    [Fact]
    public async Task AddTransactionsAsync_WithNullList_ShouldReturnFailure()
    {
        // Arrange
        List<Transaction>? transactions = null;

        // Act
        var result = await _transactionService.AddTransactionsAsync(transactions!);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AddTransactionsAsync_ShouldReturnAddedTransactions()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction("1", 100m, "11111111111"),
            CreateTransaction("2", 200m, "22222222222")
        };

        // Act
        var result = await _transactionService.AddTransactionsAsync(transactions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data![0].Cpf.Should().Be("11111111111");
        result.Data[1].Cpf.Should().Be("22222222222");
    }

    #endregion

    #region ClearAllDataAsync Tests

    [Fact]
    public async Task ClearAllDataAsync_ShouldRemoveAllTransactions()
    {
        // Arrange
        await SeedTransactions(
            CreateTransaction("1", 100m, "11111111111"),
            CreateTransaction("2", 200m, "22222222222"),
            CreateTransaction("3", 300m, "33333333333")
        );

        var beforeCount = await _context.Transactions.CountAsync();
        beforeCount.Should().Be(3);

        // Act
        var result = await _transactionService.ClearAllDataAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var afterCount = await _context.Transactions.CountAsync();
        afterCount.Should().Be(0);
    }

    [Fact]
    public async Task ClearAllDataAsync_WithEmptyDatabase_ShouldSucceed()
    {
        // Act
        var result = await _transactionService.ClearAllDataAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static Transaction CreateTransaction(string natureCode, decimal amount, string cpf)
    {
        return new Transaction
        {
            NatureCode = natureCode,
            Amount = amount,
            Cpf = cpf,
            Card = "1234****5678",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = new TimeSpan(12, 0, 0),
            BankCode = natureCode
        };
    }

    private async Task SeedTransactions(params Transaction[] transactions)
    {
        await _context.Transactions.AddRangeAsync(transactions);
        await _context.SaveChangesAsync();
    }

    #endregion
}
