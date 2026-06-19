using FairwayFinder.Features.Data;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PreferredTees).InclusiveBetween(0, 1);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.DeviceName).MaximumLength(200);
    }
}
