using FluentValidation;
using Recam.Services.DTOs;

namespace Recam.Services.Validators.Auth;

public class RegisterPhotographyCompanyRequestValidator : AbstractValidator<RegisterPhotographyCompanyRequest>
{
    public RegisterPhotographyCompanyRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required").EmailAddress().WithMessage("Email is invalid")
            .MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required").MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");
        RuleFor(x => x.CompanyName).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.CompanyName));
    }
}