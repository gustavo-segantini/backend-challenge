using CnabApi.Common;
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

    #region GetTransactionsByCpfAsync Tests

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithExistingCpf_ShouldReturnTransactions()
    {
        // Arrange
        var targetCpf = "11111111111";
        await SeedTransactions(
            CreateTransaction("1", 100m, targetCpf),
            CreateTransaction("2", 200m, targetCpf),
            CreateTransaction("3", 300m, "99999999999") // Different CPF
        );

        // Act
        var result = await _transactionService.GetTransactionsByCpfAsync(BuildOptions(targetCpf));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.Items.Should().OnlyContain(t => t.Cpf == targetCpf);
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithNonExistingCpf_ShouldReturnEmptyList()
    {
        // Arrange
        await SeedTransactions(
            CreateTransaction("1", 100m, "11111111111")
        );

        // Act
        var result = await _transactionService.GetTransactionsByCpfAsync(BuildOptions("99999999999"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_ShouldOrderByDateDescending()
    {
        // Arrange
        var cpf = "11111111111";
        var oldTransaction = CreateTransaction("1", 100m, cpf);
        oldTransaction.TransactionDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var newTransaction = CreateTransaction("2", 200m, cpf);
        newTransaction.TransactionDate = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        await SeedTransactions(oldTransaction, newTransaction);

        // Act
        var result = await _transactionService.GetTransactionsByCpfAsync(BuildOptions(cpf));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items[0].TransactionDate.Should().BeAfter(result.Data.Items[1].TransactionDate);
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithEmptyCpf_ShouldReturnFailure()
    {
        // Act
        var result = await _transactionService.GetTransactionsByCpfAsync(BuildOptions(""));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("CPF");
    }

    [Fact]
    public async Task GetTransactionsByCpfAsync_WithWhitespaceCpf_ShouldReturnFailure()
    {
        // Act
        var result = await _transactionService.GetTransactionsByCpfAsync(BuildOptions("   "));

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetBalanceByCpfAsync Tests

    [Fact]
    public async Task GetBalanceByCpfAsync_WithIncomeTransactions_ShouldReturnPositiveBalance()
    {
        // Arrange
        var cpf = "11111111111";
        await SeedTransactions(
            CreateTransaction("1", 100m, cpf), // Income: +100
            CreateTransaction("4", 50m, cpf),  // Income: +50
            CreateTransaction("5", 30m, cpf)   // Income: +30
        );

        // Act
        var result = await _transactionService.GetBalanceByCpfAsync(cpf);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(180m);
    }

    [Fact]
    public async Task GetBalanceByCpfAsync_WithExpenseTransactions_ShouldReturnNegativeBalance()
    {
        // Arrange
        var cpf = "11111111111";
        await SeedTransactions(
            CreateTransaction("2", 100m, cpf), // Expense: -100
            CreateTransaction("3", 50m, cpf),  // Expense: -50
            CreateTransaction("9", 30m, cpf)   // Expense: -30
        );

        // Act
        var result = await _transactionService.GetBalanceByCpfAsync(cpf);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(-180m);
    }

    [Fact]
    public async Task GetBalanceByCpfAsync_WithMixedTransactions_ShouldReturnCorrectBalance()
    {
        // Arrange
        var cpf = "11111111111";
        await SeedTransactions(
            CreateTransaction("1", 1000m, cpf),  // Income: +1000
            CreateTransaction("2", 300m, cpf),   // Expense: -300
            CreateTransaction("4", 500m, cpf),   // Income: +500
            CreateTransaction("9", 200m, cpf)    // Expense: -200
        );

        // Act
        var result = await _transactionService.GetBalanceByCpfAsync(cpf);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(1000m); // 1000 + 500 - 300 - 200 = 1000
    }

    [Fact]
    public async Task GetBalanceByCpfAsync_WithNoTransactions_ShouldReturnZero()
    {
        // Arrange
        var cpf = "11111111111";

        // Act
        var result = await _transactionService.GetBalanceByCpfAsync(cpf);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(0m);
    }

    [Fact]
    public async Task GetBalanceByCpfAsync_ShouldOnlyConsiderTargetCpfTransactions()
    {
        // Arrange
        var targetCpf = "11111111111";
        var otherCpf = "99999999999";
        
        await SeedTransactions(
            CreateTransaction("1", 1000m, targetCpf), // Should count
            CreateTransaction("1", 5000m, otherCpf),  // Should NOT count
            CreateTransaction("2", 200m, targetCpf)   // Should count
        );

        // Act
        var result = await _transactionService.GetBalanceByCpfAsync(targetCpf);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(800m); // 1000 - 200 = 800 (não 5800)
    }

    [Fact]
    public async Task GetBalanceByCpfAsync_WithEmptyCpf_ShouldReturnFailure()
    {
        // Act
        var result = await _transactionService.GetBalanceByCpfAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("CPF");
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

    private static TransactionQueryOptions BuildOptions(string cpf, int page = 1, int pageSize = 50)
    {
        return new TransactionQueryOptions
        {
            Cpf = cpf,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task SeedTransactions(params Transaction[] transactions)
    {
        await _context.Transactions.AddRangeAsync(transactions);
        await _context.SaveChangesAsync();
    }

    #endregion
}
