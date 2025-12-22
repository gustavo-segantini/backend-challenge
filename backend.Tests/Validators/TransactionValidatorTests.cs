using FluentValidation.TestHelper;
using CnabApi.Models;
using CnabApi.Validators;

namespace CnabApi.Tests.Validators;

public class TransactionValidatorTests
{
    private readonly TransactionValidator _validator;

    public TransactionValidatorTests()
    {
        _validator = new TransactionValidator();
    }

    private static Transaction CreateValidTransaction()
    {
        return new Transaction
        {
            Cpf = "11144477735",
            BankCode = "1234",
            NatureCode = "123456",
            Amount = 100.50m,
            Card = "1234567890",
            StoreOwner = "12345678901234",
            StoreName = "Store Name",
            TransactionDate = DateTime.UtcNow.AddHours(-1)
        };
    }

    #region CPF Validation Tests

    [Fact]
    public void Validate_WithEmptyCpf_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Cpf = string.Empty;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Cpf)
            .WithErrorMessage("CPF is required");
    }

    [Fact]
    public void Validate_WithNullCpf_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Cpf = null;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Cpf)
            .WithErrorMessage("CPF is required");
    }

    [Fact]
    public void Validate_WithCpfWrongLength_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Cpf = "123456789";

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Cpf)
            .WithErrorMessage("CPF must have exactly 11 digits");
    }

    [Fact]
    public void Validate_WithCpfContainingNonDigits_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Cpf = "123.456.789-01";

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Cpf)
            .WithErrorMessage("CPF must contain only digits");
    }

    [Fact]
    public void Validate_WithCpfAllSameDigits_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Cpf = "11111111111";

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Cpf)
            .WithErrorMessage("CPF is invalid according to CPF algorithm");
    }

    [Fact]
    public void Validate_WithInvalidCpfChecksum_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Cpf = "12345678900"; // Invalid checksum

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Cpf)
            .WithErrorMessage("CPF is invalid according to CPF algorithm");
    }

    #endregion

    #region BankCode Validation Tests

    [Fact]
    public void Validate_WithEmptyBankCode_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.BankCode = string.Empty;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.BankCode)
            .WithErrorMessage("Bank code is required");
    }

    [Fact]
    public void Validate_WithNullBankCode_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.BankCode = null;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.BankCode)
            .WithErrorMessage("Bank code is required");
    }

    [Fact]
    public void Validate_WithBankCodeTooLong_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.BankCode = "12345";

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.BankCode)
            .WithErrorMessage("Bank code must be between 1 and 4 characters");
    }

    [Fact]
    public void Validate_WithValidBankCode_ReturnsSuccess()
    {
        var transaction = CreateValidTransaction();
        transaction.BankCode = "1";

        var result = _validator.TestValidate(transaction);

        result.ShouldNotHaveValidationErrorFor(t => t.BankCode);
    }

    #endregion

    #region NatureCode Validation Tests

    [Fact]
    public void Validate_WithEmptyNatureCode_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.NatureCode = string.Empty;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.NatureCode)
            .WithErrorMessage("Nature code is required");
    }

    [Fact]
    public void Validate_WithNullNatureCode_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.NatureCode = null;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.NatureCode)
            .WithErrorMessage("Nature code is required");
    }

    [Fact]
    public void Validate_WithNatureCodeTooLong_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.NatureCode = "1234567890123";

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.NatureCode)
            .WithErrorMessage("Nature code must be between 1 and 12 characters");
    }

    [Fact]
    public void Validate_WithValidNatureCode_ReturnsSuccess()
    {
        var transaction = CreateValidTransaction();
        transaction.NatureCode = "1";

        var result = _validator.TestValidate(transaction);

        result.ShouldNotHaveValidationErrorFor(t => t.NatureCode);
    }

    #endregion

    #region Amount Validation Tests

    [Fact]
    public void Validate_WithZeroAmount_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Amount = 0;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Amount)
            .WithErrorMessage("Amount must be greater than 0");
    }

    [Fact]
    public void Validate_WithNegativeAmount_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Amount = -100;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Amount)
            .WithErrorMessage("Amount must be greater than 0");
    }

    [Fact]
    public void Validate_WithPositiveAmount_ReturnsSuccess()
    {
        var transaction = CreateValidTransaction();
        transaction.Amount = 0.01m;

        var result = _validator.TestValidate(transaction);

        result.ShouldNotHaveValidationErrorFor(t => t.Amount);
    }

    #endregion

    #region Card Validation Tests

    [Fact]
    public void Validate_WithEmptyCard_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Card = string.Empty;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Card)
            .WithErrorMessage("Card is required");
    }

    [Fact]
    public void Validate_WithNullCard_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Card = null;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Card)
            .WithErrorMessage("Card is required");
    }

    [Fact]
    public void Validate_WithCardTooLong_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.Card = "1234567890123";

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Card)
            .WithErrorMessage("Card must be between 1 and 12 characters");
    }

    [Fact]
    public void Validate_WithValidCard_ReturnsSuccess()
    {
        var transaction = CreateValidTransaction();
        transaction.Card = "1";

        var result = _validator.TestValidate(transaction);

        result.ShouldNotHaveValidationErrorFor(t => t.Card);
    }

    #endregion

    #region StoreOwner Validation Tests

    [Fact]
    public void Validate_WithEmptyStoreOwner_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.StoreOwner = string.Empty;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.StoreOwner)
            .WithErrorMessage("Store owner is required");
    }

    [Fact]
    public void Validate_WithNullStoreOwner_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.StoreOwner = null;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.StoreOwner)
            .WithErrorMessage("Store owner is required");
    }

    [Fact]
    public void Validate_WithStoreOwnerTooLong_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.StoreOwner = "123456789012345"; // 15 characters

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.StoreOwner)
            .WithErrorMessage("Store owner must not exceed 14 characters");
    }

    [Fact]
    public void Validate_WithValidStoreOwner_ReturnsSuccess()
    {
        var transaction = CreateValidTransaction();
        transaction.StoreOwner = "12345678901234"; // 14 characters

        var result = _validator.TestValidate(transaction);

        result.ShouldNotHaveValidationErrorFor(t => t.StoreOwner);
    }

    #endregion

    #region StoreName Validation Tests

    [Fact]
    public void Validate_WithEmptyStoreName_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.StoreName = string.Empty;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.StoreName)
            .WithErrorMessage("Store name is required");
    }

    [Fact]
    public void Validate_WithNullStoreName_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.StoreName = null;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.StoreName)
            .WithErrorMessage("Store name is required");
    }

    [Fact]
    public void Validate_WithStoreNameTooLong_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.StoreName = "123456789012345678901"; // 21 characters

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.StoreName)
            .WithErrorMessage("Store name must not exceed 18 characters");
    }

    [Fact]
    public void Validate_WithValidStoreName_ReturnsSuccess()
    {
        var transaction = CreateValidTransaction();
        transaction.StoreName = "123456789012345678"; // 18 characters

        var result = _validator.TestValidate(transaction);

        result.ShouldNotHaveValidationErrorFor(t => t.StoreName);
    }

    #endregion

    #region TransactionDate Validation Tests

    [Fact]
    public void Validate_WithEmptyTransactionDate_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.TransactionDate = default;

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.TransactionDate)
            .WithErrorMessage("Transaction date is required");
    }

    [Fact]
    public void Validate_WithFutureTransactionDate_ReturnsFail()
    {
        var transaction = CreateValidTransaction();
        transaction.TransactionDate = DateTime.UtcNow.AddHours(1);

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.TransactionDate)
            .WithErrorMessage("Transaction date cannot be in the future");
    }

    [Fact]
    public void Validate_WithPastTransactionDate_ReturnsSuccess()
    {
        var transaction = CreateValidTransaction();
        transaction.TransactionDate = DateTime.UtcNow.AddDays(-1);

        var result = _validator.TestValidate(transaction);

        result.ShouldNotHaveValidationErrorFor(t => t.TransactionDate);
    }

    #endregion

    #region Complete Transaction Validation Tests

    [Fact]
    public void Validate_WithAllValidData_ReturnsSuccess()
    {
        var transaction = CreateValidTransaction();

        var result = _validator.TestValidate(transaction);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        var transaction = new Transaction
        {
            Cpf = string.Empty,
            BankCode = string.Empty,
            NatureCode = string.Empty,
            Amount = 0,
            Card = string.Empty,
            StoreOwner = string.Empty,
            StoreName = string.Empty,
            TransactionDate = default
        };

        var result = _validator.TestValidate(transaction);

        result.ShouldHaveValidationErrorFor(t => t.Cpf);
        result.ShouldHaveValidationErrorFor(t => t.BankCode);
        result.ShouldHaveValidationErrorFor(t => t.NatureCode);
        result.ShouldHaveValidationErrorFor(t => t.Amount);
        result.ShouldHaveValidationErrorFor(t => t.Card);
        result.ShouldHaveValidationErrorFor(t => t.StoreOwner);
        result.ShouldHaveValidationErrorFor(t => t.StoreName);
        result.ShouldHaveValidationErrorFor(t => t.TransactionDate);
    }

    #endregion
}
