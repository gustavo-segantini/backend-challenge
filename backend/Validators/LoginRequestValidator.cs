using FluentValidation;
using CnabApi.Models.Requests;

namespace CnabApi.Validators;

/// <summary>
/// Validator for LoginRequest model.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(u => u.Username)
            .NotEmpty()
            .WithMessage("Username is required");

        RuleFor(u => u.Password)
            .NotEmpty()
            .WithMessage("Password is required");
    }
}
