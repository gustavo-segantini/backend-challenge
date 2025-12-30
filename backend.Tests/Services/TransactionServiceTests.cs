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

    #region AddSingleTransactionAsync Tests

    [Fact]
    public async Task AddSingleTransactionAsync_WithValidTransaction_ShouldSaveToDatabase()
    {
        // Arrange
        var transaction = CreateTransaction("1", 100m, "11144477735");
        transaction.IdempotencyKey = "unique-key-1";

        // Act
        var result = await _transactionService.AddSingleTransactionAsync(transaction);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        var saved = await _context.Transactions.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.IdempotencyKey.Should().Be("unique-key-1");
    }

    [Fact]
    public async Task AddSingleTransactionAsync_WithNullTransaction_ShouldReturnFailure()
    {
        // Act
        var result = await _transactionService.AddSingleTransactionAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Transaction cannot be null");
    }

    [Fact]
    public async Task AddSingleTransactionAsync_WithDuplicateIdempotencyKey_ShouldReturnFailure()
    {
        // Arrange
        var transaction1 = CreateTransaction("1", 100m, "11144477735");
        transaction1.IdempotencyKey = "duplicate-key";
        await _transactionService.AddSingleTransactionAsync(transaction1);

        var transaction2 = CreateTransaction("2", 200m, "22222222222");
        transaction2.IdempotencyKey = "duplicate-key"; // Same key

        // Act
        var result = await _transactionService.AddSingleTransactionAsync(transaction2);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Transaction already exists");
    }

    #endregion

    #region AddTransactionToContextAsync Tests

    [Fact]
    public async Task AddTransactionToContextAsync_WithValidTransaction_ShouldAddToContext()
    {
        // Arrange
        var transaction = CreateTransaction("1", 100m, "11144477735");
        transaction.IdempotencyKey = "context-key-1";

        // Act
        var result = await _transactionService.AddTransactionToContextAsync(transaction);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        
        // Should be in context but not saved yet
        var inContext = _context.ChangeTracker.Entries<Transaction>()
            .FirstOrDefault(e => e.Entity.IdempotencyKey == "context-key-1");
        inContext.Should().NotBeNull();
        inContext!.State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task AddTransactionToContextAsync_WithNullTransaction_ShouldReturnFailure()
    {
        // Act
        var result = await _transactionService.AddTransactionToContextAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Transaction cannot be null");
    }

    [Fact]
    public async Task AddTransactionToContextAsync_WithDuplicateIdempotencyKey_ShouldReturnFailure()
    {
        // Arrange
        var transaction1 = CreateTransaction("1", 100m, "11144477735");
        transaction1.IdempotencyKey = "duplicate-context-key";
        await _transactionService.AddSingleTransactionAsync(transaction1);

        var transaction2 = CreateTransaction("2", 200m, "22222222222");
        transaction2.IdempotencyKey = "duplicate-context-key"; // Same key

        // Act
        var result = await _transactionService.AddTransactionToContextAsync(transaction2);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Transaction already exists");
    }

    [Fact]
    public async Task AddTransactionToContextAsync_ShouldNotSaveChanges()
    {
        // Arrange
        var transaction = CreateTransaction("1", 100m, "11144477735");
        transaction.IdempotencyKey = "no-save-key";

        // Act
        await _transactionService.AddTransactionToContextAsync(transaction);

        // Assert - Should not be saved to database yet
        var saved = await _context.Transactions.FirstOrDefaultAsync();
        saved.Should().BeNull();
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
            BankCode = natureCode,
            StoreOwner = "Test Owner",
            StoreName = "Test Store",
            IdempotencyKey = Guid.NewGuid().ToString()
        };
    }

    private async Task SeedTransactions(params Transaction[] transactions)
    {
        await _context.Transactions.AddRangeAsync(transactions);
        await _context.SaveChangesAsync();
    }

    #endregion
}
