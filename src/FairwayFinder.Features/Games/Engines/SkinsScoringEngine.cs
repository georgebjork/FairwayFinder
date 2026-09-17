using FairwayFinder.Data.Entities;

namespace FairwayFinder.Features.Games.Engines;

/// <summary>
/// Skins: each hole is worth a skin to whoever wins it outright. A tied hole carries its skin into
/// the next one when the game plays carryover, otherwise the skin is simply void.
///
/// The walk stops at the first unsettled hole (see
/// <see cref="GameScoringContext.SettledHoleNumbers"/>) — carryover must never leap a gap, because
/// a skin can only ride onto the next hole if the current one really is finished.
/// </summary>
public sealed class SkinsScoringEngine : GameScoringEngine<SkinsScoreboard>
{
    public override GameType GameType => GameType.Skins;

    public override SkinsScoreboard Score(GameScoringContext context)
    {
        var useNet = context.Rules.UseNet;
        var settled = context.SettledHoleNumbers;

        var won = context.Participants.ToDictionary(p => p.ParticipantId, _ => 0);
        var holes = new List<SkinsHole>(settled.Count);
        var carried = 0;

        foreach (var holeNumber in settled)
        {
            var par = ParOn(context, holeNumber);
            var lines = context.Participants
                .Select(p => (p.ParticipantId, Score: p.Holes[holeNumber].Effective(useNet)!.Value))
                .ToList();

            var low = lines.Min(l => l.Score);
            var winners = lines.Where(l => l.Score == low).ToList();
            var carriedIn = carried;

            if (winners.Count == 1)
            {
                var awarded = 1 + carriedIn;
                won[winners[0].ParticipantId] += awarded;
                carried = 0;

                holes.Add(new SkinsHole(holeNumber, par, winners[0].ParticipantId, awarded, carriedIn, IsTied: false));
            }
            else
            {
                // Ties carry when the game says so; otherwise the skin is gone, not banked.
                carried = context.Rules.SkinsCarryover ? carriedIn + 1 : 0;

                holes.Add(new SkinsHole(holeNumber, par, WonByParticipantId: null, SkinsAwarded: 0, carriedIn, IsTied: true));
            }
        }

        var tallies = context.Participants
            .Select(p => new SkinsTally(
                p.ParticipantId,
                p.DisplayName,
                won[p.ParticipantId],
                context.Rules.SkinsValue is { } value ? won[p.ParticipantId] * value : null))
            .OrderByDescending(t => t.SkinsWon)
            .ThenBy(t => t.DisplayName)
            .ToList();

        var holesRemaining = context.HoleNumbers.Count - settled.Count;

        return new SkinsScoreboard
        {
            HolesPlayed = settled.Count,
            HolesRemaining = holesRemaining,
            IsDecided = holesRemaining == 0,
            Summary = BuildSummary(tallies, carried, settled.Count),
            Standings = BuildStandings(tallies),
            CarriedSkins = carried,
            Holes = holes,
            Tallies = tallies
        };
    }

    /// <summary>
    /// Par comes from a participant's own teebox, so a mixed-tee field has no single par for a
    /// hole. The card shows the first one, which is what the hosting group's scorecard says.
    /// </summary>
    private static int ParOn(GameScoringContext context, int holeNumber)
        => context.Participants[0].Holes[holeNumber].Par;

    private static List<GameStanding> BuildStandings(IReadOnlyList<SkinsTally> tallies)
    {
        var standings = new List<GameStanding>(tallies.Count);
        var leadSkins = tallies.Count == 0 ? 0 : tallies[0].SkinsWon;

        var position = 0;
        var seen = 0;
        int? previous = null;

        foreach (var tally in tallies)
        {
            seen++;
            // Standard competition ranking: ties share a position, the next one skips.
            if (previous is null || tally.SkinsWon != previous) position = seen;
            previous = tally.SkinsWon;

            standings.Add(new GameStanding(
                tally.ParticipantId,
                tally.DisplayName,
                position,
                tally.SkinsWon == 1 ? "1 skin" : $"{tally.SkinsWon} skins",
                IsLeader: leadSkins > 0 && tally.SkinsWon == leadSkins));
        }

        return standings;
    }

    private static string BuildSummary(IReadOnlyList<SkinsTally> tallies, int carried, int holesPlayed)
    {
        if (holesPlayed == 0) return "No holes scored yet";

        if (carried > 0)
        {
            return carried == 1
                ? $"1 skin on the line thru {holesPlayed}"
                : $"{carried} skins on the line thru {holesPlayed}";
        }

        var leadSkins = tallies[0].SkinsWon;
        if (leadSkins == 0) return $"No skins won thru {holesPlayed}";

        var leaders = tallies.Where(t => t.SkinsWon == leadSkins).ToList();
        var noun = leadSkins == 1 ? "skin" : "skins";

        return leaders.Count == 1
            ? $"{leaders[0].DisplayName} {leadSkins} {noun} thru {holesPlayed}"
            : $"{string.Join(" and ", leaders.Select(l => l.DisplayName))} tied on {leadSkins} {noun} thru {holesPlayed}";
    }
}
