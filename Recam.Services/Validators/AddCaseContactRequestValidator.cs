using FluentValidation;
using Recam.Services.DTOs;

namespace Recam.Services.Validators;

public class AddCaseContactRequestValidator : AbstractValidator<AddCaseContactRequest>
{
    public AddCaseContactRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CompanyName).MaximumLength(200).When(x => x.CompanyName != null);
        RuleFor(x => x.ProfileUrl).MaximumLength(1000).When(x => x.ProfileUrl != null);
    }
}