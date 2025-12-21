using CnabApi.Models;
using FluentAssertions;

namespace CnabApi.Tests.Models;

/// <summary>
/// Unit tests for the Transaction model.
/// Tests the business logic for transaction description and signed amount calculations.
/// </summary>
public class TransactionTests
{
    #region TransactionDescription Tests

    [Theory]
    [InlineData("1", "Debit")]
    [InlineData("2", "Boleto")]
    [InlineData("3", "Financing")]
    [InlineData("4", "Credit")]
    [InlineData("5", "Loan Receipt")]
    [InlineData("6", "Sales")]
    [InlineData("7", "TED Receipt")]
    [InlineData("8", "DOC Receipt")]
    [InlineData("9", "Rent")]
    [InlineData("0", "Transaction")]
    [InlineData("X", "Transaction")]
    public void TransactionDescription_ShouldReturnCorrectDescription(string natureCode, string expectedDescription)
    {
        // Arrange
        var transaction = new Transaction { NatureCode = natureCode };

        // Act
        var description = transaction.TransactionDescription;

        // Assert
        description.Should().Be(expectedDescription);
    }

    #endregion

    #region SignedAmount Tests - Income Transactions

    [Theory]
    [InlineData("1")] // Debit
    [InlineData("4")] // Credit
    [InlineData("5")] // Loan Receipt
    [InlineData("6")] // Sales
    [InlineData("7")] // TED Receipt
    [InlineData("8")] // DOC Receipt
    public void SignedAmount_WhenIncomeType_ShouldReturnPositiveAmount(string natureCode)
    {
        // Arrange
        var transaction = new Transaction 
        { 
            NatureCode = natureCode,
            Amount = 100m 
        };

        // Act
        var signedAmount = transaction.SignedAmount;

        // Assert
        signedAmount.Should().Be(100m);
    }

    #endregion

    #region SignedAmount Tests - Expense Transactions

    [Theory]
    [InlineData("2")] // Boleto
    [InlineData("3")] // Financing
    [InlineData("9")] // Rent
    public void SignedAmount_WhenExpenseType_ShouldReturnNegativeAmount(string natureCode)
    {
        // Arrange
        var transaction = new Transaction 
        { 
            NatureCode = natureCode,
            Amount = 100m 
        };

        // Act
        var signedAmount = transaction.SignedAmount;

        // Assert
        signedAmount.Should().Be(-100m);
    }

    #endregion

    #region SignedAmount Tests - Neutral/Unknown Types

    [Theory]
    [InlineData("0")]
    [InlineData("X")]
    [InlineData("")]
    public void SignedAmount_WhenNeutralType_ShouldReturnZero(string natureCode)
    {
        // Arrange
        var transaction = new Transaction 
        { 
            NatureCode = natureCode,
            Amount = 100m 
        };

        // Act
        var signedAmount = transaction.SignedAmount;

        // Assert
        signedAmount.Should().Be(0m);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void Transaction_WhenCreated_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var transaction = new Transaction();

        // Assert
        transaction.BankCode.Should().BeEmpty();
        transaction.Cpf.Should().BeEmpty();
        transaction.NatureCode.Should().BeEmpty();
        transaction.Card.Should().BeEmpty();
        transaction.Amount.Should().Be(0);
        transaction.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Balance Calculation Tests

    [Fact]
    public void SignedAmount_WithMultipleTransactions_ShouldCalculateCorrectBalance()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new() { NatureCode = "1", Amount = 100m },  // +100 (Debit/Income)
            new() { NatureCode = "2", Amount = 30m },   // -30 (Boleto/Expense)
            new() { NatureCode = "4", Amount = 50m },   // +50 (Credit/Income)
            new() { NatureCode = "9", Amount = 20m },   // -20 (Rent/Expense)
        };

        // Act
        var totalBalance = transactions.Sum(t => t.SignedAmount);

        // Assert
        totalBalance.Should().Be(100m); // 100 - 30 + 50 - 20 = 100
    }

    [Fact]
    public void SignedAmount_WithOnlyIncomeTransactions_ShouldReturnPositiveBalance()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new() { NatureCode = "1", Amount = 100m },
            new() { NatureCode = "4", Amount = 200m },
            new() { NatureCode = "7", Amount = 300m },
        };

        // Act
        var totalBalance = transactions.Sum(t => t.SignedAmount);

        // Assert
        totalBalance.Should().Be(600m);
    }

    [Fact]
    public void SignedAmount_WithOnlyExpenseTransactions_ShouldReturnNegativeBalance()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            new() { NatureCode = "2", Amount = 100m },
            new() { NatureCode = "3", Amount = 200m },
            new() { NatureCode = "9", Amount = 300m },
        };

        // Act
        var totalBalance = transactions.Sum(t => t.SignedAmount);

        // Assert
        totalBalance.Should().Be(-600m);
    }

    #endregion

    #region Specific Amount Tests

    [Fact]
    public void SignedAmount_WithDecimalAmount_ShouldPreservePrecision()
    {
        // Arrange
        var transaction = new Transaction 
        { 
            NatureCode = "1",
            Amount = 123.45m 
        };

        // Act
        var signedAmount = transaction.SignedAmount;

        // Assert
        signedAmount.Should().Be(123.45m);
    }

    [Fact]
    public void SignedAmount_WithLargeAmount_ShouldHandleCorrectly()
    {
        // Arrange
        var transaction = new Transaction 
        { 
            NatureCode = "2",
            Amount = 999999999.99m 
        };

        // Act
        var signedAmount = transaction.SignedAmount;

        // Assert
        signedAmount.Should().Be(-999999999.99m);
    }

    [Fact]
    public void SignedAmount_WithZeroAmount_ShouldReturnZero()
    {
        // Arrange
        var transaction = new Transaction 
        { 
            NatureCode = "1",
            Amount = 0m 
        };

        // Act
        var signedAmount = transaction.SignedAmount;

        // Assert
        signedAmount.Should().Be(0m);
    }

    #endregion
}
