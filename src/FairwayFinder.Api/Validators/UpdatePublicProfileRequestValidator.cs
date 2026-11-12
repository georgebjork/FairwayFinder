using FairwayFinder.Api.Endpoints;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class UpdatePublicProfileRequestValidator : AbstractValidator<UpdatePublicProfileRequest>
{
    public UpdatePublicProfileRequestValidator()
    {
        // The full slug rules live in PublicSlugRules and are enforced by the service, so the admin
        // console gets them too; this only stops the obvious.
        RuleFor(x => x.Slug).MaximumLength(64);
    }
}
