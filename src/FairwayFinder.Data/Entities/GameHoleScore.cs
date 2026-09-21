namespace FairwayFinder.Data.Entities;

/// <summary>
/// A stroke count the host entered for a participant who has no round of their own.
///
/// Deliberately not a shadow <see cref="Round"/>: <c>ix_round_user_id_active</c> is keyed on
/// <c>user_id</c>, so a guest would need a synthetic one, and those rounds would leak into round
/// lists and stats. A four-column table avoids all of it.
///
/// Keys on <see cref="HoleNumber"/> rather than a hole id (as <see cref="Score"/> does) because
/// hole numbers survive a change of tees and hole ids do not: <c>hole</c> rows hang off a teebox,
/// so Blue's 4th and White's 4th are different rows. An admin moving a guest between tees would
/// otherwise strand every row on the old teebox — par and stroke index read from the new tee,
/// hole identity from the old. The cost is no foreign key on the hole; membership is enforced on
/// write instead (<c>HoleNotInGame</c>) and by validating teebox coverage when a player joins.
/// </summary>
public class GameHoleScore
{
    public long GameHoleScoreId { get; set; }

    public long GameParticipantId { get; set; }

    public int HoleNumber { get; set; }

    public short Strokes { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual GameParticipant GameParticipant { get; set; } = null!;
}
