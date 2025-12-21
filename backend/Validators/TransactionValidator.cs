using FluentValidation;
using CnabApi.Models;

namespace CnabApi.Validators;

/// <summary>
/// Validator for Transaction model with CPF validation.
/// </summary>
public class TransactionValidator : AbstractValidator<Transaction>
{
    public TransactionValidator()
    {
        RuleFor(t => t.Cpf)
            .NotEmpty()
            .WithMessage("CPF is required")
            .Length(11)
            .WithMessage("CPF must have exactly 11 digits")
            .Matches(@"^\d+$")
            .WithMessage("CPF must contain only digits")
            .Custom((cpf, context) =>
            {
                if (!IsValidCpf(cpf))
                {
                    context.AddFailure("CPF is invalid according to CPF algorithm");
                }
            });

        RuleFor(t => t.BankCode)
            .NotEmpty()
            .WithMessage("Bank code is required")
            .Length(1, 4)
            .WithMessage("Bank code must be between 1 and 4 characters");

        RuleFor(t => t.NatureCode)
            .NotEmpty()
            .WithMessage("Nature code is required")
            .Length(1, 12)
            .WithMessage("Nature code must be between 1 and 12 characters");

        RuleFor(t => t.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0");

        RuleFor(t => t.Card)
            .NotEmpty()
            .WithMessage("Card is required")
            .Length(1, 12)
            .WithMessage("Card must be between 1 and 12 characters");

        RuleFor(t => t.StoreOwner)
            .NotEmpty()
            .WithMessage("Store owner is required")
            .MaximumLength(14)
            .WithMessage("Store owner must not exceed 14 characters");

        RuleFor(t => t.StoreName)
            .NotEmpty()
            .WithMessage("Store name is required")
            .MaximumLength(18)
            .WithMessage("Store name must not exceed 18 characters");

        RuleFor(t => t.TransactionDate)
            .NotEmpty()
            .WithMessage("Transaction date is required")
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Transaction date cannot be in the future");
    }

    /// <summary>
    /// Validates a CPF using the official CPF algorithm.
    /// </summary>
    private static bool IsValidCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11)
            return false;

        // Check if all digits are the same (invalid CPFs)
        if (cpf.All(c => c == cpf[0]))
            return false;

        // First check digit
        int sum = 0;
        int remainder;

        for (int i = 1; i <= 9; i++)
            sum += int.Parse(cpf[i - 1].ToString()) * (11 - i);

        remainder = (sum * 10) % 11;

        if (remainder == 10 || remainder == 11)
            remainder = 0;

        if (remainder != int.Parse(cpf[9].ToString()))
            return false;

        // Second check digit
        sum = 0;
        for (int i = 1; i <= 10; i++)
            sum += int.Parse(cpf[i - 1].ToString()) * (12 - i);

        remainder = (sum * 10) % 11;

        if (remainder == 10 || remainder == 11)
            remainder = 0;

        if (remainder != int.Parse(cpf[10].ToString()))
            return false;

        return true;
    }
}
