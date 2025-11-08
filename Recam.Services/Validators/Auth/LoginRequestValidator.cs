using FluentValidation;
using Microsoft.AspNetCore.Identity.Data;

namespace Recam.Services.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required").EmailAddress();
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
    }
}