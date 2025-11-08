using FluentValidation;
using Recam.Services.DTOs;

namespace Recam.Services.Validators.Listings;

public class UpdateListingCaseRequestValidator : AbstractValidator<UpdateListingCaseRequest>
{
    public UpdateListingCaseRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(255);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).InclusiveBetween(0, 999999);

        RuleFor(x => x.Bedrooms).InclusiveBetween(0, 20);
        RuleFor(x => x.Bathrooms).InclusiveBetween(0, 20);
        RuleFor(x => x.Garages).InclusiveBetween(0, 10);

        RuleFor(x => x.FloorArea).GreaterThan(0).When(x => x.FloorArea.HasValue);
        RuleFor(x => x.Price).GreaterThan(0).When(x => x.Price.HasValue);

        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}