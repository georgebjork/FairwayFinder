using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class CreateGameRequestValidator : AbstractValidator<CreateGameRequest>
{
    public CreateGameRequestValidator()
    {
        RuleFor(x => x.GameType).IsInEnum();
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.TeeboxId).GreaterThan(0);
        RuleFor(x => x.DatePlayed).NotEmpty();

        RuleFor(x => x.HandicapAllowancePercent)
            .InclusiveBetween(1, 100)
            .WithMessage("Handicap allowance must be between 1 and 100 percent.");

        RuleFor(x => x.CourseHandicap).SetValidator(new CourseHandicapValidator());

        RuleFor(x => x)
            .Must(r => r.FullRound || r.FrontNine || r.BackNine)
            .WithMessage("Specify FullRound, FrontNine, or BackNine.");

        RuleFor(x => x.SkinsValue)
            .GreaterThan(0)
            .When(x => x.SkinsValue is not null)
            .WithMessage("A skin has to be worth something.");
    }
}

public class JoinGameRequestValidator : AbstractValidator<JoinGameRequest>
{
    public JoinGameRequestValidator()
    {
        RuleFor(x => x.JoinCode).NotEmpty().MaximumLength(12);
        RuleFor(x => x.TeeboxId).GreaterThan(0);
        RuleFor(x => x.CourseHandicap).SetValidator(new CourseHandicapValidator());
    }
}

public class AddParticipantRequestValidator : AbstractValidator<AddParticipantRequest>
{
    public AddParticipantRequestValidator()
    {
        RuleFor(x => x.TeeboxId).GreaterThan(0);
        RuleFor(x => x.CourseHandicap).SetValidator(new CourseHandicapValidator());

        // A participant is either a registered golfer or a named guest — never neither.
        RuleFor(x => x)
            .Must(r => !string.IsNullOrWhiteSpace(r.UserId) || !string.IsNullOrWhiteSpace(r.DisplayName))
            .WithMessage("Supply a UserId for a friend, or a DisplayName for a guest.");

        RuleFor(x => x.DisplayName).MaximumLength(128);
    }
}

public class UpsertGameHoleRequestValidator : AbstractValidator<UpsertGameHoleRequest>
{
    public UpsertGameHoleRequestValidator()
    {
        RuleFor(x => x.Strokes).GreaterThan((short)0);
    }
}

/// <summary>
/// A course handicap for a game is the strokes a player receives over the holes that game covers,
/// so a nine-hole game carries a nine-hole number. The range is wide enough for a plus player at
/// one end and a maximum index at the other.
/// </summary>
internal class CourseHandicapValidator : AbstractValidator<int>
{
    public CourseHandicapValidator()
    {
        RuleFor(x => x)
            .InclusiveBetween(-10, 54)
            .WithMessage("Course handicap must be between -10 and 54 strokes for the holes this game plays.");
    }
}
