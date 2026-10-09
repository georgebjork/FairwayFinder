using FairwayFinder.Data.Entities;

namespace FairwayFinder.Features.Games.Engines;

/// <summary>
/// High-Low between two equal teams. Each hole is worth two points: one for the better low ball
/// (each side's best score) and one for the better high ball (each side's worst score). A tied
/// point goes to nobody and does not carry.
///
/// Unlike match play nothing freezes. Every hole's points count, so the board is decided early only
/// in the sense that the winner can no longer change — the margin keeps moving until the last hole.
/// </summary>
public sealed class HighLowScoringEngine : GameScoringEngine<HighLowScoreboard>
{
    private const int PointsPerHole = 2;

    public override GameType GameType => GameType.HighLow;

    public override HighLowScoreboard Score(GameScoringContext context)
    {
        var useNet = context.Rules.UseNet;
        var settled = context.SettledHoleNumbers;
        var holesRemaining = context.HoleNumbers.Count - settled.Count;

        var sides = GameSides.Resolve(context.Participants);
        if (!IsPlayableField(sides))
        {
            // The field is validated when the game starts, so this only fires for a game still
            // being built.
            return Empty(settled.Count, holesRemaining);
        }

        var (first, second) = (sides![0], sides[1]);

        var firstPoints = 0;
        var secondPoints = 0;
        var holes = new List<HighLowHole>(settled.Count);

        foreach (var holeNumber in settled)
        {
            var firstLow = GameSides.BestBall(first, holeNumber, useNet);
            var secondLow = GameSides.BestBall(second, holeNumber, useNet);
            var firstHigh = GameSides.WorstBall(first, holeNumber, useNet);
            var secondHigh = GameSides.WorstBall(second, holeNumber, useNet);

            var lowWinner = Winner(firstLow, secondLow, first, second);
            var highWinner = Winner(firstHigh, secondHigh, first, second);

            var firstOnHole = PointsFor(first, lowWinner, highWinner);
            var secondOnHole = PointsFor(second, lowWinner, highWinner);
            firstPoints += firstOnHole;
            secondPoints += secondOnHole;

            holes.Add(new HighLowHole(
                holeNumber,
                Par: first.Members[0].Holes[holeNumber].Par,
                Sides:
                [
                    HoleSide(first, holeNumber, useNet, firstLow, firstHigh, firstOnHole),
                    HoleSide(second, holeNumber, useNet, secondLow, secondHigh, secondOnHole)
                ],
                LowWonBySideParticipantId: lowWinner,
                HighWonBySideParticipantId: highWinner,
                RunningLeaderParticipantId: LeaderOf(firstPoints, secondPoints, first, second),
                RunningMargin: Math.Abs(firstPoints - secondPoints)));
        }

        var leader = LeaderOf(firstPoints, secondPoints, first, second);
        var margin = Math.Abs(firstPoints - secondPoints);

        // The trailing side can win at most every remaining point, so a lead bigger than that is
        // settled even with holes to play.
        var isDecided = holesRemaining == 0 || margin > PointsPerHole * holesRemaining;

        var tallies = new List<HighLowSideTally>
        {
            Tally(first, firstPoints),
            Tally(second, secondPoints)
        };

        return new HighLowScoreboard
        {
            HolesPlayed = settled.Count,
            HolesRemaining = holesRemaining,
            IsDecided = isDecided,
            Summary = BuildSummary(tallies, leader, margin, settled.Count, isDecided),
            Standings = BuildStandings(tallies, leader),
            LeaderParticipantId = leader,
            Margin = margin,
            Sides = tallies,
            Holes = holes
        };
    }

    /// <summary>Two teams, the same size, at least two a side. High ball means nothing otherwise.</summary>
    private static bool IsPlayableField(IReadOnlyList<GameSide>? sides)
        => sides is { Count: 2 }
           && sides.All(s => s.Team is not null && s.Members.Count >= 2)
           && sides[0].Members.Count == sides[1].Members.Count;

    private static long? Winner(int? first, int? second, GameSide firstSide, GameSide secondSide)
    {
        if (first is null || second is null || first == second) return null;

        return first < second ? firstSide.KeyParticipantId : secondSide.KeyParticipantId;
    }

    private static int PointsFor(GameSide side, long? lowWinner, long? highWinner)
        => (lowWinner == side.KeyParticipantId ? 1 : 0) + (highWinner == side.KeyParticipantId ? 1 : 0);

    private static HighLowHoleSide HoleSide(
        GameSide side, int holeNumber, bool useNet, int? low, int? high, int points)
    {
        var played = side.Members
            .Where(m => m.Holes.TryGetValue(holeNumber, out var h) && h.Strokes is not null)
            .ToList();

        // Which member made each number. On a tie within the side, the first listed is reported.
        var lowMember = played.FirstOrDefault(m => m.Holes[holeNumber].Effective(useNet) == low);
        var highMember = played.FirstOrDefault(m => m.Holes[holeNumber].Effective(useNet) == high);

        return new HighLowHoleSide(
            side.KeyParticipantId,
            [.. side.Members.Select(m => m.ParticipantId)],
            side.DisplayName,
            low,
            lowMember?.ParticipantId,
            high,
            highMember?.ParticipantId,
            points);
    }

    private static long? LeaderOf(int firstPoints, int secondPoints, GameSide first, GameSide second)
        => firstPoints > secondPoints ? first.KeyParticipantId
            : secondPoints > firstPoints ? second.KeyParticipantId
            : null;

    private static HighLowSideTally Tally(GameSide side, int points)
        => new(side.KeyParticipantId, [.. side.Members.Select(m => m.ParticipantId)], side.DisplayName, points);

    private static string BuildSummary(
        IReadOnlyList<HighLowSideTally> tallies, long? leader, int margin, int holesPlayed, bool isDecided)
    {
        if (holesPlayed == 0) return "No holes scored yet";

        if (leader is null)
        {
            var even = tallies[0].Points;
            return isDecided ? $"Match halved {even}–{even}" : $"All square thru {holesPlayed}";
        }

        var ahead = tallies.First(t => t.SideKeyParticipantId == leader);
        var behind = tallies.First(t => t.SideKeyParticipantId != leader);
        var score = $"{ahead.Points}–{behind.Points}";

        return isDecided
            ? $"{ahead.DisplayName} win {score}"
            : $"{ahead.DisplayName} lead by {margin} thru {holesPlayed} ({score})";
    }

    private static List<GameStanding> BuildStandings(IReadOnlyList<HighLowSideTally> tallies, long? leader)
    {
        return
        [
            .. tallies
                .OrderByDescending(t => t.Points)
                .Select(t =>
                {
                    var isLeader = leader is not null && t.SideKeyParticipantId == leader;

                    return new GameStanding(
                        t.SideKeyParticipantId,
                        t.DisplayName,
                        Position: leader is null || isLeader ? 1 : 2,
                        t.Points == 1 ? "1 pt" : $"{t.Points} pts",
                        isLeader);
                })
        ];
    }

    private static HighLowScoreboard Empty(int holesPlayed, int holesRemaining) => new()
    {
        HolesPlayed = holesPlayed,
        HolesRemaining = holesRemaining,
        IsDecided = false,
        Summary = "High-Low needs two equal teams of at least two",
        Standings = [],
        LeaderParticipantId = null,
        Margin = 0,
        Sides = [],
        Holes = []
    };
}
