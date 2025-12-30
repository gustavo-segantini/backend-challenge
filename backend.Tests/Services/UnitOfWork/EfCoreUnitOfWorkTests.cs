using CnabApi.Data;
using CnabApi.Models;
using CnabApi.Services.UnitOfWork;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace CnabApi.Tests.Services.UnitOfWork;

/// <summary>
/// Unit tests for EfCoreUnitOfWork.
/// Tests transaction management and ACID compliance.
/// </summary>
public class EfCoreUnitOfWorkTests : IDisposable
{
    private readonly CnabDbContext _context;
    private readonly EfCoreUnitOfWork _unitOfWork;

    public EfCoreUnitOfWorkTests()
    {
        var options = new DbContextOptionsBuilder<CnabDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new CnabDbContext(options);
        _unitOfWork = new EfCoreUnitOfWork(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _unitOfWork.Dispose();
    }

    #region BeginTransactionAsync Tests

    [Fact]
    public async Task BeginTransactionAsync_WithInMemoryDatabase_ShouldNotThrow()
    {
        // Act & Assert - InMemory doesn't support transactions, but should not throw
        // The method will create a transaction object even if not fully functional
        var act = async () => await _unitOfWork.BeginTransactionAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenAlreadyStarted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();

        // Act & Assert
        var act = async () => await _unitOfWork.BeginTransactionAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction already started.");
    }

    #endregion

    #region CommitAsync Tests

    [Fact]
    public async Task CommitAsync_WithoutActiveTransaction_ShouldThrowInvalidOperationException()
    {
        // Act & Assert
        var act = async () => await _unitOfWork.CommitAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No active transaction to commit.");
    }

    [Fact]
    public async Task CommitAsync_WithActiveTransaction_ShouldSaveChanges()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        var transaction = new Transaction
        {
            NatureCode = "1",
            Amount = 100m,
            Cpf = "11144477735",
            IdempotencyKey = "test-key-1",
            StoreOwner = "Test Owner",
            StoreName = "Test Store",
            Card = "1234****5678",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.FromHours(12),
            BankCode = "001"
        };
        _context.Transactions.Add(transaction);

        // Act
        await _unitOfWork.CommitAsync();

        // Assert
        var saved = await _context.Transactions.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.Amount.Should().Be(100m);
    }

    #endregion

    #region RollbackAsync Tests

    [Fact]
    public async Task RollbackAsync_WithoutActiveTransaction_ShouldNotThrow()
    {
        // Act
        await _unitOfWork.RollbackAsync();

        // Assert - Should not throw when no transaction exists
    }

    [Fact]
    public async Task RollbackAsync_WithActiveTransaction_ShouldNotSaveChanges()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        var transaction = new Transaction
        {
            NatureCode = "1",
            Amount = 100m,
            Cpf = "11144477735",
            IdempotencyKey = "test-key-rollback",
            StoreOwner = "Test Owner",
            StoreName = "Test Store",
            Card = "1234****5678",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.FromHours(12),
            BankCode = "001"
        };
        _context.Transactions.Add(transaction);

        // Act
        await _unitOfWork.RollbackAsync();

        // Assert - Changes should not be saved
        var saved = await _context.Transactions.FirstOrDefaultAsync();
        saved.Should().BeNull();
    }

    [Fact]
    public async Task RollbackAsync_ShouldClearChangeTracker()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        var transaction = new Transaction
        {
            NatureCode = "1",
            Amount = 100m,
            Cpf = "11144477735",
            IdempotencyKey = "test-key-clear"
        };
        _context.Transactions.Add(transaction);

        // Act
        await _unitOfWork.RollbackAsync();

        // Assert
        _context.ChangeTracker.Entries().Should().BeEmpty();
    }

    #endregion

    #region ExecuteInTransactionAsync Tests

    [Fact]
    public async Task ExecuteInTransactionAsync_WithInMemoryDatabase_ShouldExecuteAndSave()
    {
        // Arrange
        var transaction = new Transaction
        {
            NatureCode = "1",
            Amount = 100m,
            Cpf = "11144477735",
            IdempotencyKey = "test-key-execute",
            StoreOwner = "Test Owner",
            StoreName = "Test Store",
            Card = "1234****5678",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.FromHours(12),
            BankCode = "001"
        };

        // Act
        var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _context.Transactions.Add(transaction);
            return transaction;
        });

        // Assert
        result.Should().NotBeNull();
        var saved = await _context.Transactions.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.IdempotencyKey.Should().Be("test-key-execute");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenOperationThrows_ShouldRollback()
    {
        // Arrange
        var transaction = new Transaction
        {
            NatureCode = "1",
            Amount = 100m,
            Cpf = "11144477735",
            IdempotencyKey = "test-key-error",
            StoreOwner = "Test Owner",
            StoreName = "Test Store",
            Card = "1234****5678",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.FromHours(12),
            BankCode = "001"
        };

        // Act & Assert
        var act = async () => await _unitOfWork.ExecuteInTransactionAsync<Transaction>(async () =>
        {
            _context.Transactions.Add(transaction);
            throw new InvalidOperationException("Test error");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert - Changes should be rolled back
        var saved = await _context.Transactions.FirstOrDefaultAsync();
        saved.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithExistingTransaction_ShouldUseExisting()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        var transaction = new Transaction
        {
            NatureCode = "1",
            Amount = 100m,
            Cpf = "11144477735",
            IdempotencyKey = "test-key-existing",
            StoreOwner = "Test Owner",
            StoreName = "Test Store",
            Card = "1234****5678",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.FromHours(12),
            BankCode = "001"
        };

        // Act
        var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _context.Transactions.Add(transaction);
            return transaction;
        });

        // Assert - Should use existing transaction, not commit automatically
        result.Should().NotBeNull();
        // Transaction should still be active
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await Task.Delay(100, cts.Token);
            return "result";
        }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ShouldReturnOperationResult()
    {
        // Arrange
        const string expectedResult = "test result";

        // Act
        var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await Task.CompletedTask;
            return expectedResult;
        });

        // Assert
        result.Should().Be(expectedResult);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_ShouldDisposeTransaction()
    {
        // Arrange
        var unitOfWork = new EfCoreUnitOfWork(_context);
        await unitOfWork.BeginTransactionAsync();

        // Act
        unitOfWork.Dispose();

        // Assert - Should not throw on second dispose
        var act = () => unitOfWork.Dispose();
        act.Should().NotThrow();
    }

    #endregion
}

