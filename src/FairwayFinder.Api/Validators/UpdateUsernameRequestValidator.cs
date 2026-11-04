using FairwayFinder.Api.Endpoints;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class UpdateUsernameRequestValidator : AbstractValidator<UpdateUsernameRequest>
{
    public UpdateUsernameRequestValidator()
    {
        // The full rules (characters, periods, reserved names) live in UsernameRules and are
        // enforced by the service so the admin console gets them too; this only stops the obvious.
        RuleFor(x => x.Username).NotEmpty().MaximumLength(64);
    }
}
