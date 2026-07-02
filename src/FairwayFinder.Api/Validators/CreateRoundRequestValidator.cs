using FairwayFinder.Features.Data;
using FairwayFinder.Features.Helpers;
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

        // Shot-tracked holes carry a Shots list; validate the chain so malformed data
        // (e.g. a StartDistance = 0 placeholder row) is rejected before it corrupts strokes gained.
        // Hole-stats-only and scorecard holes have no Shots and are unaffected.
        RuleFor(x => x).Custom((hole, context) =>
        {
            if (hole.Shots is not { Count: > 0 })
                return;

            foreach (var error in StrokesGainedCalculator.ValidateShots(hole.Shots, hole.Score))
                context.AddFailure("Shots", error);
        });
    }
}
