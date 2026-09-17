using FairwayFinder.Data.Entities;

namespace FairwayFinder.Features.Games.Engines;

/// <summary>
/// Match play between exactly two sides — individuals, or teams playing a best ball. Each hole is
/// won, lost, or halved; the match is over as soon as one side leads by more holes than are left.
///
/// The result freezes at that hole. Groups routinely play the remaining holes out because a skins
/// game is riding on the same round, and a match won 4 &amp; 3 must not re-read as 1 UP because the
/// loser took the last three. Holes played after the close-out still land in
/// <see cref="MatchPlayScoreboard.Holes"/> so the card renders in full — they just cannot move the
/// result.
/// </summary>
public sealed class MatchPlayScoringEngine : GameScoringEngine<MatchPlayScoreboard>
{
    public override GameType GameType => GameType.MatchPlay;

    public override MatchPlayScoreboard Score(GameScoringContext context)
    {
        var useNet = context.Rules.UseNet;
        var settled = context.SettledHoleNumbers;
        var holesRemaining = context.HoleNumbers.Count - settled.Count;

        var sides = GameSides.Resolve(context.Participants);
        if (sides is not { Count: 2 })
        {
            // The field is validated when the game starts, so this only fires for a game still
            // being built. An empty board is the honest answer.
            return Empty(settled.Count, holesRemaining);
        }

        var (first, second) = (sides[0], sides[1]);

        // Signed toward `first`: positive means first is up.
        var holesUp = 0;
        int? decidedOnHole = null;
        var frozenUp = 0;
        var frozenRemaining = 0;

        var holes = new List<MatchPlayHole>(settled.Count);

        for (var i = 0; i < settled.Count; i++)
        {
            var holeNumber = settled[i];
            var a = GameSides.BestBall(first, holeNumber, useNet);
            var b = GameSides.BestBall(second, holeNumber, useNet);

            var delta = a < b ? 1 : a > b ? -1 : 0;
            holesUp += delta;

            var wonBy = delta switch
            {
                > 0 => first.KeyParticipantId,
                < 0 => second.KeyParticipantId,
                _ => (long?)null
            };

            holes.Add(new MatchPlayHole(
                holeNumber,
                Par: first.Members[0].Holes[holeNumber].Par,
                Sides: [HoleSide(first, holeNumber, useNet), HoleSide(second, holeNumber, useNet)],
                WonBySideParticipantId: wonBy,
                IsHalved: delta == 0,
                RunningLeaderParticipantId: LeaderOf(holesUp, first, second),
                RunningHolesUp: Math.Abs(holesUp)));

            if (decidedOnHole is null)
            {
                var remainingAfter = context.HoleNumbers.Count - (i + 1);
                if (Math.Abs(holesUp) > remainingAfter)
                {
                    decidedOnHole = holeNumber;
                    frozenUp = Math.Abs(holesUp);
                    frozenRemaining = remainingAfter;
                }
            }
        }

        // Once decided, the header reports the state at the deciding hole, not the running one.
        var isClosedOut = decidedOnHole is not null;
        var reportedUp = isClosedOut ? frozenUp : Math.Abs(holesUp);
        var reportedLeader = isClosedOut
            ? LeaderAtCloseOut(holes, decidedOnHole!.Value)
            : LeaderOf(holesUp, first, second);

        var isDecided = isClosedOut || holesRemaining == 0;
        var isDormie = !isDecided && holesRemaining > 0 && Math.Abs(holesUp) == holesRemaining;

        var resultLine = BuildResultLine(isClosedOut, frozenUp, frozenRemaining, holesRemaining, holesUp);

        return new MatchPlayScoreboard
        {
            HolesPlayed = settled.Count,
            HolesRemaining = holesRemaining,
            IsDecided = isDecided,
            Summary = BuildSummary(reportedLeader, sides, resultLine, settled.Count, isDecided, isDormie),
            Standings = BuildStandings(sides, reportedUp, reportedLeader),
            LeaderParticipantId = reportedLeader,
            HolesUp = reportedUp,
            IsDormie = isDormie,
            DecidedOnHole = decidedOnHole,
            ResultLine = resultLine,
            Holes = holes
        };
    }

    private static MatchPlayHoleSide HoleSide(GameSide side, int holeNumber, bool useNet)
    {
        // For a team, report the member whose ball counted.
        var counting = side.Members
            .Where(m => m.Holes.TryGetValue(holeNumber, out var h) && h.Strokes is not null)
            .OrderBy(m => m.Holes[holeNumber].Effective(useNet))
            .FirstOrDefault();

        var hole = counting is null ? (GameHoleLine?)null : counting.Holes[holeNumber];

        return new MatchPlayHoleSide(
            side.KeyParticipantId,
            [.. side.Members.Select(m => m.ParticipantId)],
            side.DisplayName,
            hole?.Strokes,
            hole?.Net,
            hole?.StrokesReceived ?? 0);
    }

    private static long? LeaderOf(int holesUp, GameSide first, GameSide second)
        => holesUp switch
        {
            > 0 => first.KeyParticipantId,
            < 0 => second.KeyParticipantId,
            _ => null
        };

    private static long? LeaderAtCloseOut(IReadOnlyList<MatchPlayHole> holes, int decidedOnHole)
        => holes.First(h => h.HoleNumber == decidedOnHole).RunningLeaderParticipantId;

    private static string BuildResultLine(
        bool isClosedOut, int frozenUp, int frozenRemaining, int holesRemaining, int holesUp)
    {
        if (isClosedOut)
        {
            // "4 & 3" — won by four holes with three to play. A win on the final hole is "1 UP".
            return frozenRemaining == 0 ? $"{frozenUp} UP" : $"{frozenUp} & {frozenRemaining}";
        }

        return holesUp == 0 ? "AS" : $"{Math.Abs(holesUp)} UP";
    }

    private static string BuildSummary(
        long? leader,
        IReadOnlyList<GameSide> sides,
        string resultLine,
        int holesPlayed,
        bool isDecided,
        bool isDormie)
    {
        if (holesPlayed == 0) return "No holes scored yet";

        if (leader is null)
        {
            return isDecided ? "Match halved" : $"All square thru {holesPlayed}";
        }

        var name = sides.First(s => s.KeyParticipantId == leader).DisplayName;

        if (isDecided) return $"{name} wins {resultLine}";

        return isDormie
            ? $"{name} {resultLine} and dormie thru {holesPlayed}"
            : $"{name} {resultLine} thru {holesPlayed}";
    }

    private static List<GameStanding> BuildStandings(
        IReadOnlyList<GameSide> sides, int reportedUp, long? leader)
    {
        return
        [
            .. sides.Select(side =>
            {
                var isLeader = leader is not null && side.KeyParticipantId == leader;
                var value = leader is null ? "AS" : isLeader ? $"{reportedUp} UP" : $"{reportedUp} DN";

                return new GameStanding(
                    side.KeyParticipantId,
                    side.DisplayName,
                    Position: leader is null ? 1 : isLeader ? 1 : 2,
                    value,
                    isLeader);
            })
        ];
    }

    private static MatchPlayScoreboard Empty(int holesPlayed, int holesRemaining) => new()
    {
        HolesPlayed = holesPlayed,
        HolesRemaining = holesRemaining,
        IsDecided = false,
        Summary = "Match play needs exactly two sides",
        Standings = [],
        LeaderParticipantId = null,
        HolesUp = 0,
        IsDormie = false,
        DecidedOnHole = null,
        ResultLine = "AS",
        Holes = []
    };
}
