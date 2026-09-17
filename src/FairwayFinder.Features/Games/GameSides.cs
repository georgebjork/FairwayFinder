namespace FairwayFinder.Features.Games;

/// <summary>One competing side: a lone golfer, or a team playing a best ball.</summary>
public sealed record GameSide(int? Team, IReadOnlyList<GameParticipantLine> Members)
{
    /// <summary>Identifies the side on the wire. For a team, its first member.</summary>
    public long KeyParticipantId => Members[0].ParticipantId;

    public string DisplayName => Members.Count == 1
        ? Members[0].DisplayName
        : string.Join(" / ", Members.Select(m => m.DisplayName));
}

/// <summary>
/// Turning a field into sides. Match play needs this now and Nassau will need exactly the same
/// logic, so it lives beside the context rather than inside an engine.
/// </summary>
public static class GameSides
{
    /// <summary>
    /// Groups the field into sides. All-null <c>Team</c> gives one side per participant; all
    /// non-null gives one side per distinct team. A mix is ambiguous and returns null, which the
    /// caller turns into a validation failure naming the problem.
    /// </summary>
    public static IReadOnlyList<GameSide>? Resolve(IReadOnlyList<GameParticipantLine> field)
    {
        if (field.Count == 0) return [];

        var teamed = field.Count(p => p.Team is not null);
        if (teamed != 0 && teamed != field.Count) return null;

        if (teamed == 0)
        {
            return [.. field.Select(p => new GameSide(null, [p]))];
        }

        return
        [
            .. field
                .GroupBy(p => p.Team!.Value)
                .OrderBy(g => g.Key)
                .Select(g => new GameSide(g.Key, [.. g]))
        ];
    }

    /// <summary>
    /// A side's score on one hole: its best ball on the effective score. Null when nobody on the
    /// side has played the hole — which, because engines only walk settled holes, should not
    /// happen in practice.
    /// </summary>
    public static int? BestBall(GameSide side, int holeNumber, bool useNet)
    {
        int? best = null;

        foreach (var member in side.Members)
        {
            if (!member.Holes.TryGetValue(holeNumber, out var hole)) continue;

            var score = hole.Effective(useNet);
            if (score is null) continue;

            if (best is null || score < best) best = score;
        }

        return best;
    }
}
