using FairwayFinder.Shared;

namespace FairwayFinder.Data.Entities;

/// <summary>
/// One player in a game. Either a registered golfer (<see cref="UserId"/> set) or a guest the host
/// is scoring by hand.
/// </summary>
public class GameParticipant : IAuditable
{
    public long GameParticipantId { get; set; }

    public long GameId { get; set; }

    /// <summary>Null for a guest who has no account.</summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Snapshot of the player's name taken when they were added, so scoring a game never has to
    /// touch AspNetUsers. Admin repair can re-snapshot it.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Set once this participant links their own round. When set it is the source of truth for
    /// their strokes and <c>game_hole_score</c> is ignored for them.
    /// </summary>
    public long? RoundId { get; set; }

    /// <summary>
    /// Their tees — par and stroke index are read from here, not the game. Forced to the linked
    /// round's teebox whenever <see cref="RoundId"/> is set: the round's scores join to holes on
    /// that teebox, so anything else would read par from a different hole set.
    /// </summary>
    public long TeeboxId { get; set; }

    /// <summary>
    /// Strokes this player receives over the holes <em>this game covers</em> — not necessarily an
    /// 18-hole course handicap. A nine-hole game wants the nine-hole number, which is already what
    /// players negotiate on the first tee.
    /// </summary>
    public int CourseHandicap { get; set; }

    /// <summary>Side for team games. Null for an individual.</summary>
    public int? Team { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateTime UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual Game Game { get; set; } = null!;
    public virtual Teebox Teebox { get; set; } = null!;
}
