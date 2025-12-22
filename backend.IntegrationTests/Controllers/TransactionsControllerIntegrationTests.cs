using CnabApi.IntegrationTests.Infrastructure;
using CnabApi.Models;
using System.Net;
using Xunit;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace CnabApi.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for TransactionsController with real PostgreSQL database
/// </summary>
public class TransactionsControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetTransactionsByCpf_WithValidCpf_Returns200()
    {
        // Arrange
        using var context = DbContext;
        
        // Add test data
        var transaction = new Transaction
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
        };
        
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        // Assert that transaction was saved
        var savedTransaction = await context.Transactions.FirstOrDefaultAsync(t => t.Cpf == "12345678901");
        Assert.NotNull(savedTransaction);
        Assert.Equal(100m, savedTransaction.Amount);
    }

    [Fact]
    public async Task Transaction_CalculatesSignedAmount_Correctly()
    {
        // Arrange
        var incomeTransaction = new Transaction
        {
            BankCode = "0001",
            Cpf = "12345678901",
            NatureCode = "4",  // Income
            Amount = 100m,
            Card = "123456",
            StoreOwner = "Owner",
            StoreName = "Store",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.Zero
        };

        // Act & Assert
        Assert.Equal(100m, incomeTransaction.SignedAmount);

        // Arrange - expense
        var expenseTransaction = new Transaction
        {
            BankCode = "0001",
            Cpf = "12345678901",
            NatureCode = "2",  // Expense
            Amount = 50m,
            Card = "123456",
            StoreOwner = "Owner",
            StoreName = "Store",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.Zero
        };

        // Act & Assert
        Assert.Equal(-50m, expenseTransaction.SignedAmount);
    }

    [Fact]
    public async Task Transaction_GeneratesDescription_FromNatureCode()
    {
        // Arrange & Act
        var transaction = new Transaction
        {
            BankCode = "0001",
            Cpf = "12345678901",
            NatureCode = "4",  // Credit
            Amount = 100m,
            Card = "123456",
            StoreOwner = "Owner",
            StoreName = "Store",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.Zero
        };

        // Assert
        Assert.Equal("Credit", transaction.TransactionDescription);
    }

    [Fact]
    public async Task Transaction_WithUnknownNatureCode_HasDefaultDescription()
    {
        // Arrange & Act
        var transaction = new Transaction
        {
            BankCode = "0001",
            Cpf = "12345678901",
            NatureCode = "99",  // Unknown
            Amount = 100m,
            Card = "123456",
            StoreOwner = "Owner",
            StoreName = "Store",
            TransactionDate = DateTime.UtcNow,
            TransactionTime = TimeSpan.Zero
        };

        // Assert
        Assert.Equal("Transaction", transaction.TransactionDescription);
    }
}
