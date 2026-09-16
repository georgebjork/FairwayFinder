using FairwayFinder.Features.Data;
using FairwayFinder.Features.Helpers;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class StartRoundRequestValidator : AbstractValidator<StartRoundRequest>
{
    public StartRoundRequestValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.TeeboxId).GreaterThan(0);
        RuleFor(x => x.DatePlayed).NotEmpty();

        // A guard the atomic path never had: a round has to say which holes it intends to cover,
        // so the app can lay out a scorecard before any hole is played.
        RuleFor(x => x)
            .Must(r => r.FullRound || r.FrontNine || r.BackNine)
            .WithMessage("Specify FullRound, FrontNine, or BackNine.");
    }
}

public class UpsertHoleRequestValidator : AbstractValidator<UpsertHoleRequest>
{
    public UpsertHoleRequestValidator()
    {
        RuleFor(x => x.Score).GreaterThan((short)0);

        // Same shot-chain contract as HoleScoreEntryValidator. There is no Par rule here because
        // par is resolved server-side from the round's teebox, so there is nothing to check it
        // against — and nothing for a client to get wrong.
        RuleFor(x => x).Custom((hole, context) =>
        {
            if (hole.Shots is not { Count: > 0 })
                return;

            foreach (var error in StrokesGainedCalculator.ValidateShots(hole.Shots, hole.Score))
                context.AddFailure("Shots", error);
        });
    }
}
