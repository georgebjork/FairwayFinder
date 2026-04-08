using FairwayFinder.Features.Data;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class UpdateRoundRequestValidator : AbstractValidator<UpdateRoundRequest>
{
    public UpdateRoundRequestValidator()
    {
        RuleFor(x => x.RoundId).GreaterThan(0);
        RuleFor(x => x.TeeboxId).GreaterThan(0);
        RuleFor(x => x.DatePlayed).NotEmpty();
        RuleFor(x => x.Holes).NotEmpty();

        RuleForEach(x => x.Holes).SetValidator(new HoleScoreEntryValidator());
    }
}
