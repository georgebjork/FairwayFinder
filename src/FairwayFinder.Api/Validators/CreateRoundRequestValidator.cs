using FairwayFinder.Features.Data;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class CreateRoundRequestValidator : AbstractValidator<CreateRoundRequest>
{
    public CreateRoundRequestValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.TeeboxId).GreaterThan(0);
        RuleFor(x => x.DatePlayed).NotEmpty();
        RuleFor(x => x.Holes).NotEmpty();

        RuleForEach(x => x.Holes).SetValidator(new HoleScoreEntryValidator());
    }
}

public class HoleScoreEntryValidator : AbstractValidator<HoleScoreEntry>
{
    public HoleScoreEntryValidator()
    {
        RuleFor(x => x.HoleId).GreaterThan(0);
        RuleFor(x => x.HoleNumber).InclusiveBetween(1, 18);
        RuleFor(x => x.Par).InclusiveBetween(3, 6);
        RuleFor(x => x.Score).GreaterThan((short)0);
    }
}
